using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using static KlyonHubPlatform.Domain.Core.Constants.RoleNames;
using KlyonHubPlatform.Infra.Data.Context;
using KlyonHubPlatform.Infra.CrossCutting.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Api.Constants;

namespace KlyonHubPlatform.Api.Controllers
{
    /// <summary>
    /// API para cadastro rápido de cliente com endereço fiscal
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = Administrator)]
    public class QuickClientApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IContractService _contractService;
        private readonly ICoworkingService _coworkingService;
        private readonly IEmailService _emailService;
        private readonly IPaymentService _paymentService;
        private readonly ISystemConfigurationService _systemConfigurationService;
        private readonly IAuditService _auditService;
        private readonly IAppBaseUrlProvider _appBaseUrlProvider;

        public QuickClientApiController(
            ApplicationDbContext context,
            IContractService contractService,
            ICoworkingService coworkingService,
            IEmailService emailService,
            IPaymentService paymentService,
            ISystemConfigurationService systemConfigurationService,
            IAuditService auditService,
            IAppBaseUrlProvider appBaseUrlProvider)
        {
            _context = context;
            _contractService = contractService;
            _coworkingService = coworkingService;
            _emailService = emailService;
            _paymentService = paymentService;
            _systemConfigurationService = systemConfigurationService;
            _auditService = auditService;
            _appBaseUrlProvider = appBaseUrlProvider;
        }

        /// <summary>
        /// Cadastra um novo cliente, cria usuário, envia email e vincula ao endereço fiscal
        /// </summary>
        [HttpPost("Register")]
        public async Task<IActionResult> RegisterClient([FromBody] QuickClientRegistrationRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validações básicas
                if (string.IsNullOrWhiteSpace(request.ContactEmail) || string.IsNullOrWhiteSpace(request.ContactName))
                {
                    return BadRequest(new QuickClientRegistrationResponse
                    {
                        Success = false,
                        Message = ErrorJson.ContactEmailAndNameRequired
                    });
                }

                if (request.MonthlyValue <= 0)
                {
                    return BadRequest(new QuickClientRegistrationResponse
                    {
                        Success = false,
                        Message = ErrorJson.AnnualValueMustBeGreaterThanZero
                    });
                }

                if (request.CoworkingAddressId <= 0)
                {
                    return BadRequest(new QuickClientRegistrationResponse
                    {
                        Success = false,
                        Message = ErrorJson.FiscalAddressRequired
                    });
                }

                // Verificar se o endereço está disponível
                var selectedAddress = await _context.CoworkingAddresses
                    .Include(a => a.Coworking)
                    .FirstOrDefaultAsync(a => a.Id == request.CoworkingAddressId);

                if (selectedAddress == null)
                {
                    return BadRequest(new QuickClientRegistrationResponse
                    {
                        Success = false,
                        Message = ErrorJson.AddressNotFoundQuickClient
                    });
                }

                if (!selectedAddress.Coworking.IsActive)
                {
                    return BadRequest(new QuickClientRegistrationResponse
                    {
                        Success = false,
                        Message = ErrorJson.PartnerSuspendedNoNewContracts
                    });
                }

                if (selectedAddress.AddressStatus != AddressStatus.Active)
                {
                    return BadRequest(new QuickClientRegistrationResponse
                    {
                        Success = false,
                        Message = ErrorJson.AddressNotActiveForSales
                    });
                }

                if (!selectedAddress.IsAvailable || selectedAddress.CurrentContracts >= selectedAddress.MaxContracts)
                {
                    return BadRequest(new QuickClientRegistrationResponse
                    {
                        Success = false,
                        Message = ErrorJson.AddressNotAvailableForContract
                    });
                }

                // Buscar ou criar usuário
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.ContactEmail);
                User user;
                bool isNewUser = existingUser == null;

                if (existingUser != null)
                {
                    user = existingUser;
                    // Verificar se já existe cliente para este usuário
                    var existingClient = await _context.Clients.FirstOrDefaultAsync(c => c.UserId == user.Id);
                    if (existingClient != null)
                    {
                        return BadRequest(new QuickClientRegistrationResponse
                        {
                            Success = false,
                            Message = string.Format(ErrorJson.ClientAlreadyExistsWithEmail, request.ContactEmail)
                        });
                    }
                }
                else
                {
                    // Gerar senha com 4 primeiras letras do email
                    var defaultPassword = request.ContactEmail.Length >= 4
                        ? request.ContactEmail.Substring(0, 4).ToLower()
                        : request.ContactEmail.ToLower();

                    // Criar novo usuário
                    var passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword);
                    user = new User
                    {
                        Email = request.ContactEmail,
                        PasswordHash = passwordHash,
                        FullName = request.ContactName,
                        IsActive = true,
                        EmailVerified = false,
                        MustChangePassword = true
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    // Atribuir role de Client
                    var clientRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == RoleNames.Client);
                    if (clientRole != null)
                    {
                        _context.UserRoles.Add(new UserRole
                        {
                            UserId = user.Id,
                            RoleId = clientRole.Id
                        });
                        await _context.SaveChangesAsync();
                    }
                }

                // Criar cliente
                var client = new Client
                {
                    UserId = user.Id,
                    ContactEmail = request.ContactEmail,
                    ContactName = request.ContactName,
                    RazaoSocial = request.RazaoSocial,
                    NomeFantasia = request.NomeFantasia,
                    ContactPhone = request.ContactPhone?.Replace("(", "").Replace(")", "").Replace(" ", "").Replace("-", ""),
                    Address = request.Address,
                    City = request.City,
                    State = request.State,
                    ZipCode = request.ZipCode?.Replace("-", ""),
                    IsActive = true
                };

                // Processar CNPJ
                if (!string.IsNullOrWhiteSpace(request.Cnpj))
                {
                    var cnpjClean = request.Cnpj.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                    if (cnpjClean.Length == 14)
                    {
                        var existingCNPJ = await _context.Clients
                            .Where(c => c.CNPJ != null &&
                                c.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "").Trim() == cnpjClean)
                            .FirstOrDefaultAsync();

                        if (existingCNPJ != null)
                        {
                            return BadRequest(new QuickClientRegistrationResponse
                            {
                                Success = false,
                                Message = string.Format(ErrorJson.CnpjAlreadyRegistered, request.Cnpj)
                            });
                        }
                        client.CNPJ = $"{cnpjClean.Substring(0, 2)}.{cnpjClean.Substring(2, 3)}.{cnpjClean.Substring(5, 3)}/{cnpjClean.Substring(8, 4)}-{cnpjClean.Substring(12, 2)}";
                    }
                }

                // Processar CPF
                if (!string.IsNullOrWhiteSpace(request.Cpf))
                {
                    var cpfClean = request.Cpf.Replace(".", "").Replace("-", "").Trim();
                    if (cpfClean.Length == 11)
                    {
                        var existingCPF = await _context.Clients
                            .Where(c => c.CPF != null &&
                                c.CPF.Replace(".", "").Replace("-", "").Trim() == cpfClean)
                            .FirstOrDefaultAsync();

                        if (existingCPF != null)
                        {
                            return BadRequest(new QuickClientRegistrationResponse
                            {
                                Success = false,
                                Message = string.Format(ErrorJson.CpfAlreadyRegistered, request.Cpf)
                            });
                        }
                        client.CPF = $"{cpfClean.Substring(0, 3)}.{cpfClean.Substring(3, 3)}.{cpfClean.Substring(6, 3)}-{cpfClean.Substring(9, 2)}";
                    }
                }

                _context.Clients.Add(client);
                await _context.SaveChangesAsync();

                // Converter valor anual para mensal (o monthlyValue recebido é anual, precisa dividir por 12)
                var monthlyValueForContract = request.MonthlyValue / 12;

                var adminUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                // Criar contrato (venda assistida: admin responsável registrado)
                var contract = await _contractService.CreateContractAsync(
                    client.Id, 
                    request.CoworkingAddressId, 
                    monthlyValueForContract, 
                    assistedSaleAdminId: adminUserId > 0 ? adminUserId : null);

                var newValues = System.Text.Json.JsonSerializer.Serialize(new { IsAssistedSale = true, AssistedSaleAdminId = adminUserId, Origin = "VendaAssistidaAPI" });
                await _auditService.LogActionAsync(adminUserId > 0 ? adminUserId : null, "ContractCreated", "Contract", contract.Id, null, newValues,
                    HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.FirstOrDefault());

                // Buscar o pagamento criado para gerar o código PIX
                var subscription = await _context.Subscriptions
                    .FirstOrDefaultAsync(s => s.ContractId == contract.Id);

                Payment? payment = null;
                string? pixCode = null;

                if (subscription != null)
                {
                    payment = await _context.Payments
                        .Where(p => p.SubscriptionId == subscription.Id && p.Status == PaymentStatus.Pending)
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (payment != null)
                    {
                        try
                        {
                            pixCode = await _paymentService.GeneratePixCodeAsync(payment.Id);
                        }
                        catch (Exception pixEx)
                        {
                            // Se falhar ao gerar PIX, ainda retorna sucesso mas avisa
                            return Ok(new QuickClientRegistrationResponse
                            {
                                Success = true,
                                Message = $"Cliente cadastrado e contrato criado com sucesso! Contrato #{contract.ContractNumber}. Porém, o código PIX não pôde ser gerado: {pixEx.Message}",
                                ContractId = contract.Id,
                                ContractNumber = contract.ContractNumber,
                                ClientId = client.Id,
                                PaymentId = payment?.Id,
                                UserId = user.Id,
                                IsNewUser = isNewUser
                            });
                        }
                    }
                }

                // Enviar email com credenciais e link para PaymentDetails
                string? emailWarning = null;
                if (payment != null && isNewUser)
                {
                    try
                    {
                        var baseUrl = await _appBaseUrlProvider.GetBaseUrlAsync();
                        var defaultPassword = request.ContactEmail.Length >= 4
                            ? request.ContactEmail.Substring(0, 4).ToLower()
                            : request.ContactEmail.ToLower();

                        // Criar token de autenticação automática usando PasswordResetToken
                        var authToken = Guid.NewGuid().ToString();
                        user.PasswordResetToken = authToken;
                        user.PasswordResetExpires = DateTimeHelper.GetBrazilianTime().AddDays(7);
                        await _context.SaveChangesAsync();

                        var paymentDetailsUrl = $"{baseUrl}/Auth/AutoLogin?token={authToken}&redirect=/Client/PaymentDetails/{payment.Id}";

                        await _emailService.SendClientCredentialsEmailAsync(
                            request.ContactEmail,
                            request.ContactName,
                            request.ContactEmail,
                            defaultPassword,
                            paymentDetailsUrl
                        );
                    }
                    catch (Exception emailEx)
                    {
                        emailWarning = $"Houve um erro ao enviar o email: {emailEx.Message}";
                    }
                }

                var responseMessage = $"Cliente cadastrado e contrato criado com sucesso! Contrato #{contract.ContractNumber}";
                if (!string.IsNullOrEmpty(emailWarning))
                {
                    responseMessage += $" {emailWarning}";
                }

                return Ok(new QuickClientRegistrationResponse
                {
                    Success = true,
                    Message = responseMessage,
                    ContractId = contract.Id,
                    ContractNumber = contract.ContractNumber,
                    ClientId = client.Id,
                    PaymentId = payment?.Id,
                    PixCode = pixCode,
                    UserId = user.Id,
                    IsNewUser = isNewUser
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new QuickClientRegistrationResponse
                {
                    Success = false,
                    Message = $"Erro ao cadastrar cliente: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Retorna os endereços disponíveis para cadastro rápido
        /// </summary>
        [HttpGet("AvailableAddresses")]
        public async Task<IActionResult> GetAvailableAddresses(CancellationToken cancellationToken = default)
        {
            try
            {
                var availableAddresses = await _coworkingService.GetAvailableAddressesAsync();
                var addressesWithLocation = await _context.CoworkingAddresses
                    .Include(a => a.Coworking)
                    .Include(a => a.Location)
                    .Where(a => availableAddresses.Select(addr => addr.Id).Contains(a.Id))
                    .Select(a => new AvailableAddressDto
                    {
                        Id = a.Id,
                        CoworkingName = a.Coworking.Name,
                        Address = a.Address,
                        Number = a.Number,
                        City = a.Location != null ? a.Location.City : a.City,
                        State = a.Location != null ? a.Location.State : a.State,
                        BasePrice = a.BasePrice,
                        Vacancies = a.MaxContracts - a.CurrentContracts,
                        DisplayText = $"{a.Coworking.Name} - {a.Address}, {a.Number} ({(a.Location != null ? a.Location.City : a.City)}/{(a.Location != null ? a.Location.State : a.State)}) - R$ {a.BasePrice:F2}"
                    })
                    .ToListAsync();

                return Ok(addressesWithLocation);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ErrorJson.ErrorFetchingAddresses, details = ex.Message });
            }
        }
    }
}
