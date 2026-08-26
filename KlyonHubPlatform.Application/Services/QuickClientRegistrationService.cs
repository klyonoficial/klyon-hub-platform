using System.Text.Json;
using KlyonHubPlatform.Application.DTOs;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class QuickClientRegistrationService : IQuickClientRegistrationService
    {
        public const string FailureKeyEmailAndContactNameRequired = "EmailAndContactNameRequired";
        public const string FailureKeyAnnualValueMustBeGreaterThanZero = "AnnualValueMustBeGreaterThanZero";
        public const string FailureKeyAddressNotFound = "AddressNotFound";
        public const string FailureKeyPartnerSuspendedNoNewContracts = "PartnerSuspendedNoNewContracts";
        public const string FailureKeyAddressNotActiveForSales = "AddressNotActiveForSales";
        public const string FailureKeyAddressNotAvailableForContractSelected = "AddressNotAvailableForContractSelected";
        public const string FailureKeyClientAlreadyExistsWithEmail = "ClientAlreadyExistsWithEmail";
        public const string FailureKeyCnpjAlreadyRegistered = "CnpjAlreadyRegistered";
        public const string FailureKeyCpfAlreadyRegistered = "CpfAlreadyRegistered";

        private readonly ApplicationDbContext _context;
        private readonly IContractService _contractService;
        private readonly IAuditService _auditService;
        private readonly IPaymentService _paymentService;
        private readonly IEmailService _emailService;
        private readonly IAppBaseUrlProvider _appBaseUrlProvider;

        public QuickClientRegistrationService(
            ApplicationDbContext context,
            IContractService contractService,
            IAuditService auditService,
            IPaymentService paymentService,
            IEmailService emailService,
            IAppBaseUrlProvider appBaseUrlProvider)
        {
            _context = context;
            _contractService = contractService;
            _auditService = auditService;
            _paymentService = paymentService;
            _emailService = emailService;
            _appBaseUrlProvider = appBaseUrlProvider;
        }

        public async Task<QuickClientRegistrationResult> RegisterAsync(
            QuickClientRegistrationInput input,
            int? adminUserId,
            string? remoteIp,
            string? userAgent,
            CancellationToken cancellationToken = default)
        {
            var validation = await ValidateInputAsync(input, cancellationToken);
            if (validation != null)
                return validation;

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == input.ContactEmail, cancellationToken);
            User user;
            if (existingUser != null)
            {
                var existingClient = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == existingUser.Id, cancellationToken);
                if (existingClient != null)
                    return QuickClientRegistrationResult.Fail(FailureKeyClientAlreadyExistsWithEmail, input.ContactEmail);
                user = existingUser;
            }
            else
            {
                user = await CreateUserAndRoleAsync(input, cancellationToken);
            }

            var client = await CreateClientAsync(input, user.Id, cancellationToken);
            var cnpjResult = await ValidateAndSetCnpjAsync(input.Cnpj, client, cancellationToken);
            if (cnpjResult != null)
                return cnpjResult;
            var cpfResult = await ValidateAndSetCpfAsync(input.Cpf, client, cancellationToken);
            if (cpfResult != null)
                return cpfResult;

            if (!string.IsNullOrEmpty(client.ContactPhone))
                client.ContactPhone = client.ContactPhone.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", "");

            _context.Clients.Add(client);
            await _context.SaveChangesAsync(cancellationToken);

            var monthlyValueForContract = input.MonthlyValue / 12;
            var fiscalServiceId = await _context.Services.AsNoTracking()
                .Where(s => s.Code == ServiceCodes.FiscalAddressContracting)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (!fiscalServiceId.HasValue)
                return QuickClientRegistrationResult.Fail("ServiceNotFound", "Serviço de endereço fiscal não encontrado. Verifique a configuração do sistema.");
            var contract = await _contractService.CreateContractAsync(client.Id, input.CoworkingAddressId, monthlyValueForContract, fiscalServiceId.Value, assistedSaleAdminId: adminUserId, null);

            var newValues = JsonSerializer.Serialize(new { IsAssistedSale = true, AssistedSaleAdminId = adminUserId, Origin = "VendaAssistida" });
            await _auditService.LogActionAsync(adminUserId, "ContractCreated", "Contract", contract.Id, null, newValues, remoteIp, userAgent);

            string? pixCode = null;
            string? warningKey = null;
            string? warningArgument = null;
            var subscription = await _context.Subscriptions.FirstOrDefaultAsync(s => s.ContractId == contract.Id, cancellationToken);
            Payment? payment = null;
            if (subscription != null)
            {
                payment = await _context.Payments
                    .Where(p => p.SubscriptionId == subscription.Id && p.Status == PaymentStatus.Pending)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (payment != null)
                {
                    try
                    {
                        pixCode = await _paymentService.GeneratePixCodeAsync(payment.Id);
                    }
                    catch (Exception pixEx)
                    {
                        warningKey = "PixNotGenerated";
                        warningArgument = pixEx.Message;
                    }
                }
            }

            if (payment != null && existingUser == null)
            {
                try
                {
                    var baseUrl = await _appBaseUrlProvider.GetBaseUrlAsync();
                    var defaultPassword = input.ContactEmail.Length >= 4
                        ? input.ContactEmail.Substring(0, 4).ToLower()
                        : input.ContactEmail.ToLower();
                    var authToken = Guid.NewGuid().ToString();
                    user.PasswordResetToken = authToken;
                    user.PasswordResetExpires = DateTime.UtcNow.AddDays(7);
                    await _context.SaveChangesAsync(cancellationToken);
                    var paymentDetailsUrl = $"{baseUrl}/Auth/AutoLogin?token={authToken}&redirect=/Client/PaymentDetails/{payment.Id}";
                    await _emailService.SendClientCredentialsEmailAsync(
                        input.ContactEmail,
                        input.ContactName,
                        input.ContactEmail,
                        defaultPassword,
                        paymentDetailsUrl);
                }
                catch (Exception emailEx)
                {
                    if (warningKey == null) { warningKey = "EmailSendFailed"; warningArgument = emailEx.Message; }
                    else { warningArgument = $"{warningArgument}; {emailEx.Message}"; }
                }
            }

            return QuickClientRegistrationResult.Ok(
                contract.ContractNumber,
                pixCode,
                payment?.Id,
                payment != null,
                warningKey,
                warningArgument);
        }

        private async Task<QuickClientRegistrationResult?> ValidateInputAsync(QuickClientRegistrationInput input, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(input.ContactEmail) || string.IsNullOrWhiteSpace(input.ContactName))
                return QuickClientRegistrationResult.Fail(FailureKeyEmailAndContactNameRequired);
            if (input.MonthlyValue <= 0)
                return QuickClientRegistrationResult.Fail(FailureKeyAnnualValueMustBeGreaterThanZero);

            var selectedAddress = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                .FirstOrDefaultAsync(a => a.Id == input.CoworkingAddressId, cancellationToken);
            if (selectedAddress == null)
                return QuickClientRegistrationResult.Fail(FailureKeyAddressNotFound);
            if (!selectedAddress.Coworking.IsActive)
                return QuickClientRegistrationResult.Fail(FailureKeyPartnerSuspendedNoNewContracts);
            if (selectedAddress.AddressStatus != AddressStatus.Active)
                return QuickClientRegistrationResult.Fail(FailureKeyAddressNotActiveForSales);
            if (!selectedAddress.IsAvailable)
                return QuickClientRegistrationResult.Fail(FailureKeyAddressNotAvailableForContractSelected);
            return null;
        }

        private async Task<User> CreateUserAndRoleAsync(QuickClientRegistrationInput input, CancellationToken cancellationToken)
        {
            var defaultPassword = input.ContactEmail.Length >= 4
                ? input.ContactEmail.Substring(0, 4).ToLower()
                : input.ContactEmail.ToLower();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);
            var user = new User
            {
                Email = input.ContactEmail,
                PasswordHash = passwordHash,
                FullName = input.ContactName,
                IsActive = true,
                EmailVerified = false,
                MustChangePassword = true
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
            var clientRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Client, cancellationToken);
            if (clientRole != null)
            {
                _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = clientRole.Id });
                await _context.SaveChangesAsync(cancellationToken);
            }
            return user;
        }

        private static Client BuildClient(QuickClientRegistrationInput input, int userId)
        {
            return new Client
            {
                UserId = userId,
                ContactEmail = input.ContactEmail,
                ContactName = input.ContactName,
                RazaoSocial = input.RazaoSocial,
                NomeFantasia = input.NomeFantasia,
                ContactPhone = input.ContactPhone,
                Address = input.Address,
                City = input.City,
                State = input.State,
                ZipCode = input.ZipCode?.Replace("-", ""),
                IsActive = true
            };
        }

        private static Task<Client> CreateClientAsync(QuickClientRegistrationInput input, int userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(BuildClient(input, userId));
        }

        private async Task<QuickClientRegistrationResult?> ValidateAndSetCnpjAsync(string? cnpj, Client client, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(cnpj)) return null;
            var cnpjClean = cnpj.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
            if (cnpjClean.Length != 14) return null;
            var existingCNPJ = await _context.Clients
                .Where(c => c.CNPJ != null && c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingCNPJ != null)
                return QuickClientRegistrationResult.Fail(FailureKeyCnpjAlreadyRegistered, cnpj);
            client.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";
            return null;
        }

        private async Task<QuickClientRegistrationResult?> ValidateAndSetCpfAsync(string? cpf, Client client, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(cpf)) return null;
            var cpfClean = cpf.Replace(".", "").Replace("-", "").Trim();
            if (cpfClean.Length != 11) return null;
            var existingCPF = await _context.Clients
                .Where(c => c.CPF != null && c.CPF.Replace(".", "").Replace("-", "").Trim() == cpfClean)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingCPF != null)
                return QuickClientRegistrationResult.Fail(FailureKeyCpfAlreadyRegistered, cpf);
            client.CPF = $"{cpfClean.Substring(0, 3)}.{cpfClean.Substring(3, 3)}.{cpfClean.Substring(6, 3)}-{cpfClean.Substring(9, 2)}";
            return null;
        }
    }
}
