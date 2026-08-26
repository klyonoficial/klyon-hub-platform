using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using KlyonHubPlatform.Application.DTOs.FiscalOnboarding;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    public class FiscalOnboardingService : IFiscalOnboardingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAsaasService _asaasService;
        private readonly ISystemConfigurationService _systemConfig;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FiscalOnboardingService> _logger;
        private readonly IOpenCnpjQueryService _openCnpjQueryService;

        public FiscalOnboardingService(
            ApplicationDbContext context,
            IAsaasService asaasService,
            ISystemConfigurationService systemConfig,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<FiscalOnboardingService> logger,
            IOpenCnpjQueryService openCnpjQueryService)
        {
            _context = context;
            _asaasService = asaasService;
            _systemConfig = systemConfig;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
            _openCnpjQueryService = openCnpjQueryService;
        }

        public async Task<FiscalOnboardingStartResult> StartOnboardingAsync(FiscalOnboardingStartRequest request, CancellationToken cancellationToken = default)
        {
            var email = (request.ContactEmail ?? "").Trim();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal) || email.Length < 5)
                return FiscalOnboardingStartResult.Fail("Informe um e-mail válido para receber o link de acesso após o pagamento.");

            // Verificação antecipada: se e-mail, CPF ou CNPJ já estão cadastrados → bloquear e orientar login
            var emailAlreadyExists = await _context.Users.AsNoTracking()
                .AnyAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
            if (emailAlreadyExists)
                return FiscalOnboardingStartResult.Blocked(
                    FiscalOnboardingStartBlockedKind.AlreadyRegistered,
                    "Identificamos que este e-mail já possui um cadastro na plataforma. Faça login para continuar.");

            var taxDigitsEarly = Regex.Replace(request.TaxId ?? "", @"[^\d]", "");
            if (request.DeclaresHasCnpj && taxDigitsEarly.Length == 14)
            {
                var formattedCnpj = FormatCnpj(taxDigitsEarly);
                var cnpjAlreadyExists = await _context.Clients.AsNoTracking()
                    .AnyAsync(c => c.CNPJ == formattedCnpj, cancellationToken);
                if (cnpjAlreadyExists)
                    return FiscalOnboardingStartResult.Blocked(
                        FiscalOnboardingStartBlockedKind.AlreadyRegistered,
                        "Identificamos que este CNPJ já possui um cadastro na plataforma. Faça login para continuar.");
            }
            else if (!request.DeclaresHasCnpj && taxDigitsEarly.Length == 11)
            {
                var formattedCpf = FormatCpf(taxDigitsEarly);
                var cpfAlreadyExists = await _context.Clients.AsNoTracking()
                    .AnyAsync(c => c.CPF == formattedCpf, cancellationToken);
                if (cpfAlreadyExists)
                    return FiscalOnboardingStartResult.Blocked(
                        FiscalOnboardingStartBlockedKind.AlreadyRegistered,
                        "Identificamos que este CPF já possui um cadastro na plataforma. Faça login para continuar.");
            }

            var phoneDigits = Regex.Replace(request.ContactPhone ?? "", @"[^\d]", "");
            if (phoneDigits.Length < 10)
                return FiscalOnboardingStartResult.Fail("Informe o telefone ou WhatsApp com DDD (mínimo 10 dígitos). Sem telefone não é possível gerar a cobrança.");

            var taxDigits = Regex.Replace(request.TaxId ?? "", @"[^\d]", "");
            if (request.DeclaresHasCnpj)
            {
                if (taxDigits.Length != 14)
                    return FiscalOnboardingStartResult.Fail("CNPJ deve ter 14 dígitos.");
            }
            else
            {
                if (taxDigits.Length != 11)
                    return FiscalOnboardingStartResult.Fail("CPF deve ter 11 dígitos.");
            }

            if (request.DeclaresHasCnpj && taxDigits.Length == 14)
            {
                var formattedCnpj = FormatCnpj(taxDigits);
                var clientByCnpj = await _context.Clients.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CNPJ == formattedCnpj, cancellationToken);
                if (clientByCnpj != null)
                {
                    var ownerUser = await _context.Users.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == clientByCnpj.UserId, cancellationToken);
                    if (ownerUser == null)
                    {
                        _logger.LogWarning("Onboarding: cliente {ClientId} com CNPJ sem usuário associado.", clientByCnpj.Id);
                    }
                    else
                    {
                        if (!string.Equals(ownerUser.Email.Trim(), email, StringComparison.OrdinalIgnoreCase))
                        {
                            return FiscalOnboardingStartResult.Blocked(
                                FiscalOnboardingStartBlockedKind.EmailMismatchForTaxId,
                                "O e-mail informado não corresponde ao cadastrado para este CNPJ.");
                        }

                        var emailLower = email.ToLowerInvariant();
                        var pendingLeadWithPix = await FindPendingLeadWithOpenPixAsync(
                            taxDigits, emailLower, request.CoworkingAddressId, cancellationToken);
                        if (pendingLeadWithPix != null)
                            return FiscalOnboardingStartResult.Ok(pendingLeadWithPix.PublicPaymentToken);

                        if (await HasCompletedFiscalOnboardingForUserAddressAsync(
                                ownerUser.Id, taxDigits, request.CoworkingAddressId, cancellationToken))
                        {
                            return FiscalOnboardingStartResult.Blocked(
                                FiscalOnboardingStartBlockedKind.AlreadyHasFiscalAddressActive,
                                "Você já possui cadastro ativo neste endereço fiscal.");
                        }

                        if (await HasActiveContractForAddressAsync(clientByCnpj.Id, request.CoworkingAddressId, cancellationToken))
                        {
                            return FiscalOnboardingStartResult.Blocked(
                                FiscalOnboardingStartBlockedKind.AlreadyHasFiscalAddressActive,
                                "Você já possui cadastro ativo neste endereço fiscal.");
                        }
                    }
                }
            }

            var existingUserByEmail = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
            if (existingUserByEmail != null)
            {
                var fiscalAddrForUser = await GetCoworkingAddressIdForOnboardedUserAsync(existingUserByEmail.Id, cancellationToken);
                if (fiscalAddrForUser.HasValue && fiscalAddrForUser.Value == request.CoworkingAddressId)
                    return FiscalOnboardingStartResult.Fail("Este e-mail já possui cadastro. Faça login para continuar.");
            }

            if (await TaxIdRegisteredToAnotherAccountAsync(email, taxDigits, request.DeclaresHasCnpj, cancellationToken))
                return FiscalOnboardingStartResult.Fail(request.DeclaresHasCnpj
                    ? "Este CNPJ já está cadastrado em outra conta. Acesse com a conta correta."
                    : "Este CPF já está cadastrado em outra conta. Acesse com a conta correta.");

            var addressEntity = await _context.CoworkingAddresses
                .Include(a => a.Coworking)
                .FirstOrDefaultAsync(a => a.Id == request.CoworkingAddressId, cancellationToken);
            if (addressEntity == null)
                return FiscalOnboardingStartResult.Fail("Endereço não encontrado.");
            if (!addressEntity.Coworking.IsActive || addressEntity.AddressStatus != AddressStatus.Active || !addressEntity.IsAvailable)
                return FiscalOnboardingStartResult.Fail("Este endereço não está disponível para contratação no momento.");

            string? cnpjRazaoSnapshot = null;
            string? cnpjCnaeSnapshot = null;
            if (request.DeclaresHasCnpj)
            {
                var cnpjCheck = await _openCnpjQueryService.ValidateForFiscalOnboardingAsync(taxDigits, cancellationToken);
                if (!cnpjCheck.Success)
                    return FiscalOnboardingStartResult.Fail(cnpjCheck.ErrorMessage ?? "CNPJ não aprovado na validação.");
                cnpjRazaoSnapshot = cnpjCheck.RazaoSocial;
                cnpjCnaeSnapshot = cnpjCheck.CnaePrincipal;
            }

            var adhesionRaw = await _systemConfig.GetValueAsync(FiscalOnboardingConfigKey.AdhesionAmount.ToConfigKey());
            decimal adhesionAmount;
            if (!string.IsNullOrWhiteSpace(adhesionRaw) && decimal.TryParse(adhesionRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var cfgAmount) && cfgAmount > 0)
                adhesionAmount = cfgAmount;
            else
                adhesionAmount = addressEntity.BasePrice > 0 ? addressEntity.BasePrice : 1m;

            var asaasApiKey = await _systemConfig.GetValueAsync(AsaasConfigKey.ApiKey.ToConfigKey());
            if (string.IsNullOrEmpty(asaasApiKey))
                return FiscalOnboardingStartResult.Fail("Pagamentos não configurados. Tente novamente mais tarde.");

            var publicToken = Guid.NewGuid();
            var lead = new FiscalOnboardingLead
            {
                CoworkingId = addressEntity.CoworkingId,
                CoworkingAddressId = request.CoworkingAddressId,
                DeclaresHasCnpj = request.DeclaresHasCnpj,
                TaxIdNormalized = taxDigits,
                ContactEmail = email,
                ContactPhone = phoneDigits,
                CnpjRazaoSocialSnapshot = cnpjRazaoSnapshot,
                CnpjCnaePrincipalSnapshot = cnpjCnaeSnapshot,
                Status = FiscalOnboardingLeadStatus.Iniciado,
                PublicPaymentToken = publicToken,
                CreatedAt = DateTime.UtcNow
            };
            _context.FiscalOnboardingLeads.Add(lead);
            await _context.SaveChangesAsync(cancellationToken);

            try
            {
                var customerRef = $"fiscal_lead_{lead.Id}";
                var clientName = request.DeclaresHasCnpj
                    ? (!string.IsNullOrWhiteSpace(cnpjRazaoSnapshot) ? cnpjRazaoSnapshot : $"Empresa CNPJ {MaskCnpj(taxDigits)}")
                    : $"Cliente CPF {MaskCpf(taxDigits)}";
                var uf = (addressEntity.State ?? "SP").Trim().ToUpperInvariant();
                var regionCode = uf.Length >= 2 ? uf[..2] : "SP";
                var defaultAddress = new PagBankAddress
                {
                    Street = "A definir no cadastro",
                    Number = "S/N",
                    Locality = "Centro",
                    City = addressEntity.City ?? "São Paulo",
                    RegionCode = regionCode,
                    PostalCode = "01310100",
                    Country = "BRA"
                };
                var asaasCustomerId = await _asaasService.GetOrCreateAsaasCustomerIdAsync(
                    clientName,
                    email,
                    taxDigits,
                    lead.ContactPhone,
                    defaultAddress,
                    customerRef);
                lead.AsaasCustomerId = asaasCustomerId;
                lead.Status = FiscalOnboardingLeadStatus.AguardandoPagamento;
                lead.UpdatedAt = DateTime.UtcNow;

                var paymentRef = $"fiscal_ob_{lead.Id}";
                var orderResponse = await _asaasService.CreateOrderAsync(
                    referenceId: paymentRef,
                    amount: adhesionAmount,
                    method: PaymentMethod.PIX,
                    clientName: clientName,
                    clientEmail: email,
                    clientTaxId: taxDigits,
                    clientPhone: lead.ContactPhone,
                    clientAddress: defaultAddress,
                    customerExternalReference: customerRef);

                var pixCode = orderResponse.QrCodes?.FirstOrDefault()?.Text;
                var qrId = orderResponse.QrCodes?.FirstOrDefault()?.Id;
                var pixExpires = orderResponse.QrCodes?.FirstOrDefault()?.ExpirationDate;

                var payment = new Payment
                {
                    SubscriptionId = null,
                    ReservationId = null,
                    FiscalOnboardingLeadId = lead.Id,
                    ExternalPaymentId = orderResponse.Id ?? "",
                    Amount = adhesionAmount,
                    Status = PaymentStatus.Pending,
                    Method = PaymentMethod.PIX,
                    DueDate = pixExpires ?? DateTime.UtcNow.Date,
                    PixCode = pixCode,
                    PixConciliationIdentifier = string.IsNullOrEmpty(qrId) ? null : qrId,
                    PixExpiresAt = pixExpires,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync(cancellationToken);

                return FiscalOnboardingStartResult.Ok(publicToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FiscalOnboarding Start falhou para lead {LeadId}", lead.Id);
                _context.FiscalOnboardingLeads.Remove(lead);
                await _context.SaveChangesAsync(cancellationToken);
                return FiscalOnboardingStartResult.Fail($"Não foi possível gerar o PIX: {ex.Message}");
            }
        }

        public async Task<FiscalOnboardingPaymentView?> GetPaymentViewByPublicTokenAsync(Guid publicToken, CancellationToken cancellationToken = default)
        {
            var lead = await _context.FiscalOnboardingLeads
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.PublicPaymentToken == publicToken, cancellationToken);
            if (lead == null) return null;

            var payment = await _context.Payments
                .AsNoTracking()
                .Where(p => p.FiscalOnboardingLeadId == lead.Id && p.Status == PaymentStatus.Pending)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (payment == null) return null;

            var pix = payment.PixCode;
            var expires = payment.PixExpiresAt;
            if (string.IsNullOrEmpty(pix) && !string.IsNullOrEmpty(payment.ExternalPaymentId))
            {
                var qr = await _asaasService.GetPixQrCodeAsync(payment.ExternalPaymentId);
                if (qr != null && !string.IsNullOrEmpty(qr.Payload))
                {
                    pix = qr.Payload;
                    if (!string.IsNullOrEmpty(qr.ExpirationDate) && DateTime.TryParse(qr.ExpirationDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exp))
                        expires = exp;
                }
            }

            return new FiscalOnboardingPaymentView
            {
                PaymentId = payment.Id,
                PixCopyPaste = pix,
                PixExpiresAt = expires,
                Amount = payment.Amount,
                TaxIdMasked = lead.DeclaresHasCnpj ? MaskCnpj(lead.TaxIdNormalized) : MaskCpf(lead.TaxIdNormalized)
            };
        }

        public async Task HandleAdhesionPaymentConfirmedAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            var leadId = await _context.Payments.AsNoTracking()
                .Where(p => p.Id == paymentId)
                .Select(p => p.FiscalOnboardingLeadId)
                .FirstOrDefaultAsync(cancellationToken);
            if (!leadId.HasValue)
                return;

            var lead = await _context.FiscalOnboardingLeads
                .FirstOrDefaultAsync(l => l.Id == leadId.Value, cancellationToken);
            if (lead == null)
                return;
            if (lead.UserId.HasValue)
            {
                _logger.LogInformation("Onboarding: pagamento {PaymentId} já provisionado (UserId={UserId}).", paymentId, lead.UserId);
                return;
            }

            lead.Status = FiscalOnboardingLeadStatus.PagamentoConfirmado;
            lead.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Client, cancellationToken);
            if (role == null)
            {
                _logger.LogError("Role Client não encontrada ao provisionar onboarding.");
                return;
            }

            var emailKey = lead.ContactEmail.Trim().ToLowerInvariant();
            var existingUser = await _context.Users
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == emailKey, cancellationToken);

            User user;
            Client clientRow;

            if (existingUser != null)
            {
                user = existingUser;
                if (!string.IsNullOrWhiteSpace(lead.ContactPhone))
                    user.Phone = lead.ContactPhone;
                if (!user.ClientHasCnpj.HasValue)
                    user.ClientHasCnpj = lead.DeclaresHasCnpj;

                if (!user.UserRoles.Any(ur => ur.RoleId == role.Id))
                    _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

                var existingClient = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);
                if (existingClient != null)
                    clientRow = existingClient;
                else
                {
                    clientRow = new Client
                    {
                        UserId = user.Id,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Clients.Add(clientRow);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                ApplyFiscalLeadToExistingClient(lead, clientRow);
                await _context.SaveChangesAsync(cancellationToken);

                lead.UserId = user.Id;
                lead.ClientId = clientRow.Id;
                lead.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);

                await CreateMagicLoginTokenAndNotifyAsync(lead, cancellationToken);
                _logger.LogInformation("Onboarding: conta existente vinculada UserId={UserId} ClientId={ClientId} lead {LeadId}.", user.Id, clientRow.Id, lead.Id);
                return;
            }

            var randomPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(randomPassword);
            user = new User
            {
                Email = lead.ContactEmail.Trim(),
                PasswordHash = passwordHash,
                FullName = "Complete seu cadastro",
                Phone = lead.ContactPhone,
                IsActive = true,
                EmailVerified = false,
                ClientHasCnpj = lead.DeclaresHasCnpj,
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            _context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });

            clientRow = new Client
            {
                UserId = user.Id,
                RazaoSocial = lead.DeclaresHasCnpj ? lead.CnpjRazaoSocialSnapshot : null,
                CNPJ = lead.DeclaresHasCnpj ? FormatCnpj(lead.TaxIdNormalized) : null,
                CPF = lead.DeclaresHasCnpj ? null : FormatCpf(lead.TaxIdNormalized),
                ContactEmail = lead.ContactEmail,
                ContactPhone = lead.ContactPhone,
                FiscalOnboardingStatus = ClientFiscalOnboardingStatus.PagamentoAdesaoConfirmado,
                CreatedAt = DateTime.UtcNow
            };
            _context.Clients.Add(clientRow);
            await _context.SaveChangesAsync(cancellationToken);

            lead.UserId = user.Id;
            lead.ClientId = clientRow.Id;
            lead.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            await CreateMagicLoginTokenAndNotifyAsync(lead, cancellationToken);
            _logger.LogInformation("Onboarding: provisionado UserId={UserId} ClientId={ClientId} para lead {LeadId}.", user.Id, clientRow.Id, lead.Id);
        }

        public async Task<User?> TryConsumeMagicLoginTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var row = await _context.ClientMagicLoginTokens
                .Include(t => t.FiscalOnboardingLead)
                .FirstOrDefaultAsync(t => t.Token == token.Trim(), cancellationToken);
            if (row == null || row.UsedAt.HasValue || row.ExpiresAt < DateTime.UtcNow)
                return null;

            var userId = row.FiscalOnboardingLead.UserId;
            if (!userId.HasValue)
                return null;

            row.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            return await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);
        }

        public async Task<int?> GetCoworkingAddressIdForOnboardedUserAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.FiscalOnboardingLeads.AsNoTracking()
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.Id)
                .Select(l => (int?)l.CoworkingAddressId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<FiscalOnboardingLead?> FindPendingLeadWithOpenPixAsync(
            string taxDigits,
            string emailLower,
            int coworkingAddressId,
            CancellationToken cancellationToken)
        {
            var leadId = await _context.FiscalOnboardingLeads.AsNoTracking()
                .Where(l => l.TaxIdNormalized == taxDigits
                    && l.CoworkingAddressId == coworkingAddressId
                    && l.ContactEmail.ToLower() == emailLower
                    && l.Status == FiscalOnboardingLeadStatus.AguardandoPagamento)
                .OrderByDescending(l => l.Id)
                .Select(l => (int?)l.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (!leadId.HasValue)
                return null;

            var hasPendingPayment = await _context.Payments.AsNoTracking()
                .AnyAsync(p => p.FiscalOnboardingLeadId == leadId.Value && p.Status == PaymentStatus.Pending, cancellationToken);
            if (!hasPendingPayment)
                return null;

            return await _context.FiscalOnboardingLeads.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == leadId.Value, cancellationToken);
        }

        private async Task<bool> HasCompletedFiscalOnboardingForUserAddressAsync(
            int userId,
            string taxDigits,
            int coworkingAddressId,
            CancellationToken cancellationToken)
        {
            return await _context.FiscalOnboardingLeads.AsNoTracking()
                .AnyAsync(l => l.UserId == userId
                    && l.CoworkingAddressId == coworkingAddressId
                    && l.TaxIdNormalized == taxDigits
                    && (l.Status == FiscalOnboardingLeadStatus.PagamentoConfirmado
                        || l.Status == FiscalOnboardingLeadStatus.AcessoGerado), cancellationToken);
        }

        private async Task<bool> HasActiveContractForAddressAsync(
            int clientId,
            int coworkingAddressId,
            CancellationToken cancellationToken)
        {
            return await _context.Contracts.AsNoTracking()
                .AnyAsync(c => c.ClientId == clientId
                    && c.CoworkingAddressId == coworkingAddressId
                    && c.Status == ContractStatus.Active, cancellationToken);
        }

        /// <summary>CPF/CNPJ já usado por outro usuário (e-mail diferente).</summary>
        private async Task<bool> TaxIdRegisteredToAnotherAccountAsync(string normalizedEmail, string digits, bool isCnpj, CancellationToken cancellationToken)
        {
            if (isCnpj)
            {
                var formatted = FormatCnpj(digits);
                var row = await _context.Clients.AsNoTracking()
                    .Where(c => c.CNPJ == formatted)
                    .Join(_context.Users.AsNoTracking(), c => c.UserId, u => u.Id, (c, u) => u.Email)
                    .FirstOrDefaultAsync(cancellationToken);
                if (row == null)
                    return false;
                return !string.Equals(row.Trim(), normalizedEmail.Trim(), StringComparison.OrdinalIgnoreCase);
            }

            var formattedCpf = FormatCpf(digits);
            var rowCpf = await _context.Clients.AsNoTracking()
                .Where(c => c.CPF == formattedCpf)
                .Join(_context.Users.AsNoTracking(), c => c.UserId, u => u.Id, (c, u) => u.Email)
                .FirstOrDefaultAsync(cancellationToken);
            if (rowCpf == null)
                return false;
            return !string.Equals(rowCpf.Trim(), normalizedEmail.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyFiscalLeadToExistingClient(FiscalOnboardingLead lead, Client client)
        {
            client.ContactEmail = lead.ContactEmail;
            client.ContactPhone = lead.ContactPhone;
            client.FiscalOnboardingStatus = ClientFiscalOnboardingStatus.PagamentoAdesaoConfirmado;
            client.UpdatedAt = DateTime.UtcNow;

            if (lead.DeclaresHasCnpj)
            {
                var formatted = FormatCnpj(lead.TaxIdNormalized);
                if (string.IsNullOrWhiteSpace(client.CNPJ))
                    client.CNPJ = formatted;
                else if (!string.Equals(client.CNPJ, formatted, StringComparison.Ordinal))
                    _logger.LogWarning("Onboarding: CNPJ do lead difere do cliente {ClientId}; mantendo cadastro existente.", client.Id);

                if (string.IsNullOrWhiteSpace(client.RazaoSocial) && !string.IsNullOrWhiteSpace(lead.CnpjRazaoSocialSnapshot))
                    client.RazaoSocial = lead.CnpjRazaoSocialSnapshot;
            }
            else
            {
                var formattedCpf = FormatCpf(lead.TaxIdNormalized);
                if (string.IsNullOrWhiteSpace(client.CPF))
                    client.CPF = formattedCpf;
                else if (!string.Equals(client.CPF, formattedCpf, StringComparison.Ordinal))
                    _logger.LogWarning("Onboarding: CPF do lead difere do cliente {ClientId}; mantendo cadastro existente.", client.Id);
            }
        }

        private async Task CreateMagicLoginTokenAndNotifyAsync(FiscalOnboardingLead lead, CancellationToken cancellationToken)
        {
            var rawTokenBytes = RandomNumberGenerator.GetBytes(48);
            var tokenString = Convert.ToBase64String(rawTokenBytes).Replace("+", "-", StringComparison.Ordinal).Replace("/", "_", StringComparison.Ordinal).TrimEnd('=');
            var magic = new ClientMagicLoginToken
            {
                FiscalOnboardingLeadId = lead.Id,
                Token = tokenString,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };
            _context.ClientMagicLoginTokens.Add(magic);
            lead.Status = FiscalOnboardingLeadStatus.AcessoGerado;
            await _context.SaveChangesAsync(cancellationToken);

            var baseUrl = (_configuration["AppSettings:BaseUrl"] ?? "https://localhost:5001").TrimEnd('/');
            var loginUrl = $"{baseUrl}/Auth/AutoLogin?token={Uri.EscapeDataString(tokenString)}";
            var waLink = BuildWhatsAppMagicLink(lead.ContactPhone, loginUrl);
            var waParagraph = waLink != null
                ? $@"<p>Ou abra no <strong>WhatsApp</strong>: <a href=""{waLink}"">clique aqui para enviar o link ao seu número</a>.</p>"
                : "";
            var body = $@"<p>Seu pagamento de adesão foi confirmado.</p>
<p>Acesse o link abaixo para entrar automaticamente e concluir seu cadastro:</p>
<p><a href=""{loginUrl}"">{loginUrl}</a></p>
{waParagraph}
<p>O link expira em 7 dias e só pode ser usado uma vez.</p>";
            try
            {
                await _emailService.SendEmailAsync(lead.ContactEmail, "Klyon — Acesse sua conta (endereço fiscal)", body, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao enviar e-mail de magic link para {Email}", lead.ContactEmail);
            }
        }

        private static string FormatCnpj(string digits)
        {
            if (digits.Length != 14) return digits;
            return $"{digits.Substring(0, 2)}.{digits.Substring(2, 3)}.{digits.Substring(5, 3)}/{digits.Substring(8, 4)}-{digits.Substring(12, 2)}";
        }

        private static string FormatCpf(string digits)
        {
            if (digits.Length != 11) return digits;
            return $"{digits.Substring(0, 3)}.{digits.Substring(3, 3)}.{digits.Substring(6, 3)}-{digits.Substring(9, 2)}";
        }

        private static string MaskCnpj(string digits)
        {
            if (digits.Length != 14) return "***.***.***/****-**";
            return $"**.***.***/****-{digits.Substring(12, 2)}";
        }

        private static string MaskCpf(string digits)
        {
            if (digits.Length != 11) return "***.***.***-**";
            return $"***.***.*{digits.Substring(6, 2)}-{digits.Substring(9, 2)}";
        }

        /// <summary>Link wa.me com texto pré-preenchido (abre conversa com o próprio número do cliente).</summary>
        private static string? BuildWhatsAppMagicLink(string? phoneDigits, string loginUrl)
        {
            var d = Regex.Replace(phoneDigits ?? "", @"[^\d]", "");
            if (d.Length < 10)
                return null;
            if (d.Length <= 11 && !d.StartsWith("55", StringComparison.Ordinal))
                d = "55" + d;
            var text = Uri.EscapeDataString("Acesse sua conta Klyon (endereço fiscal): " + loginUrl);
            return $"https://wa.me/{d}?text={text}";
        }
    }
}
