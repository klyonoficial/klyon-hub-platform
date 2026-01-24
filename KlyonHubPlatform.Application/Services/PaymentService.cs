using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Repositories;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Interfaces;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace KlyonHubPlatform.Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly ICommissionService _commissionService;
        private readonly IContractRepository _contractRepository;
        private readonly ICoworkingRepository _coworkingRepository;
        private readonly IPagBankService _pagBankService;
        private readonly ApplicationDbContext _context;
        private readonly IAddressDeclarationService? _addressDeclarationService;
        private readonly ISystemConfigurationService _systemConfigurationService;
        private readonly IAuditService? _auditService;
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public PaymentService(
            IPaymentRepository paymentRepository,
            ISubscriptionRepository subscriptionRepository,
            ICommissionService commissionService,
            IContractRepository contractRepository,
            ICoworkingRepository coworkingRepository,
            IPagBankService pagBankService,
            ApplicationDbContext context,
            ISystemConfigurationService systemConfigurationService,
            IAddressDeclarationService? addressDeclarationService = null,
            IAuditService? auditService = null,
            IHttpContextAccessor? httpContextAccessor = null)
        {
            _paymentRepository = paymentRepository;
            _subscriptionRepository = subscriptionRepository;
            _commissionService = commissionService;
            _contractRepository = contractRepository;
            _coworkingRepository = coworkingRepository;
            _pagBankService = pagBankService;
            _context = context;
            _addressDeclarationService = addressDeclarationService;
            _systemConfigurationService = systemConfigurationService;
            _auditService = auditService;
            _httpContextAccessor = httpContextAccessor;
        }

        private async Task LogAuditAsync(int? userId, string action, string entityType, int? entityId, object? oldValues = null, object? newValues = null)
        {
            if (_auditService == null) return;

            string? ipAddress = null;
            string? userAgent = null;
            if (_httpContextAccessor?.HttpContext != null)
            {
                ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString();
                userAgent = _httpContextAccessor.HttpContext.Request.Headers["User-Agent"].ToString();
            }

            string? oldValuesJson = oldValues != null ? JsonSerializer.Serialize(oldValues) : null;
            string? newValuesJson = newValues != null ? JsonSerializer.Serialize(newValues) : null;

            await _auditService.LogActionAsync(userId, action, entityType, entityId, oldValuesJson, newValuesJson, ipAddress, userAgent);
        }

        public async Task<Payment> CreatePaymentAsync(int subscriptionId, decimal amount, PaymentMethod method)
        {
            var subscription = await _subscriptionRepository.GetByIdAsync(subscriptionId);
            if (subscription == null)
                throw new InvalidOperationException("Assinatura não encontrada");

            var payment = new Payment
            {
                SubscriptionId = subscriptionId,
                Amount = amount,
                Method = method,
                Status = PaymentStatus.Pending,
                DueDate = subscription.NextBillingDate,
                ExternalPaymentId = Guid.NewGuid().ToString()
            };

            if (method == PaymentMethod.PIX)
            {
                payment.PixExpiresAt = DateTime.UtcNow.AddHours(24);
            }

            return await _paymentRepository.CreateAsync(payment);
        }

        public async Task<Payment> ProcessPaymentAsync(int paymentId)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var payment = await _paymentRepository.GetByIdAsync(paymentId);
                    if (payment == null)
                        throw new InvalidOperationException("Pagamento não encontrado");

                    // TODO: Integrar com gateway de pagamento real (Stripe, PagSeguro, etc.)
                    // Por enquanto, simula processamento
                    payment.Status = PaymentStatus.Processing;
                    payment.UpdatedAt = DateTime.UtcNow;
                    _context.Payments.Update(payment);

                    // Simular processamento assíncrono
                    await Task.Delay(1000);

                    payment.Status = PaymentStatus.Paid;
                    payment.PaidAt = DateTime.UtcNow;
                    payment.TransactionId = Guid.NewGuid().ToString();
                    payment.UpdatedAt = DateTime.UtcNow;
                    _context.Payments.Update(payment);

                    // Atualizar assinatura
                    if (payment.SubscriptionId.HasValue)
                    {
                        var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
                        if (subscription != null)
                        {
                            subscription.LastBillingDate = DateTime.UtcNow;
                            subscription.NextBillingDate = subscription.NextBillingDate.AddMonths(1);
                            subscription.FailedPaymentAttempts = 0;
                            if (subscription.Status == SubscriptionStatus.PastDue)
                            {
                                subscription.Status = SubscriptionStatus.Active;
                            }
                            subscription.UpdatedAt = DateTime.UtcNow;
                            _context.Subscriptions.Update(subscription);

                            // Ativar contrato se for o primeiro pagamento
                            var contract = await _context.Contracts
                                .Include(c => c.CoworkingAddress)
                                .FirstOrDefaultAsync(c => c.Id == subscription.ContractId);

                            if (contract != null && (contract.Status == ContractStatus.Suspended || contract.Status == ContractStatus.PendingPayment))
                            {
                                // Verificar se é o primeiro pagamento (não há pagamentos anteriores pagos)
                                var previousPaidPayments = await _context.Payments
                                    .Where(p => p.SubscriptionId == subscription.Id && 
                                               p.Id != paymentId && 
                                               p.Status == PaymentStatus.Paid)
                                    .CountAsync();

                                if (previousPaidPayments == 0)
                                {
                                    // Guardar o status original antes de alterar
                                    var originalStatus = contract.Status;
                                    
                                    // Primeiro pagamento - ativar contrato e definir validade de 1 ano
                                    contract.Status = ContractStatus.Active;
                                    contract.ExpiryDate = DateTime.UtcNow.AddYears(1); // Validade de 1 ano a partir do pagamento
                                    contract.UpdatedAt = DateTime.UtcNow;
                                    _context.Contracts.Update(contract);

                                    // Atualizar contador de contratos do endereço apenas se estava aguardando pagamento
                                    // (contratos suspensos já foram contados anteriormente)
                                    if (contract.CoworkingAddress != null && originalStatus == ContractStatus.PendingPayment)
                                    {
                                        contract.CoworkingAddress.CurrentContracts++;
                                        if (contract.CoworkingAddress.CurrentContracts >= contract.CoworkingAddress.MaxContracts)
                                        {
                                            contract.CoworkingAddress.IsAvailable = false;
                                        }
                                        _context.CoworkingAddresses.Update(contract.CoworkingAddress);
                                    }

                                    // Gerar declaração automaticamente após o primeiro pagamento
                                    if (_addressDeclarationService != null && contract.ExpiryDate.HasValue)
                                    {
                                        try
                                        {
                                            await _addressDeclarationService.GenerateDeclarationAsync(contract.Id);
                                        }
                                        catch (Exception ex)
                                        {
                                            // Log do erro mas não impede a confirmação do pagamento
                                            System.Diagnostics.Debug.WriteLine($"Erro ao gerar declaração automaticamente: {ex.Message}");
                                        }
                                    }
                                }
                                else
                                {
                                    // Renovação - estender validade por mais 1 ano
                                    if (contract.ExpiryDate.HasValue)
                                    {
                                        contract.ExpiryDate = contract.ExpiryDate.Value.AddYears(1);
                                    }
                                    else
                                    {
                                        contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                                    }
                                    contract.UpdatedAt = DateTime.UtcNow;
                                    _context.Contracts.Update(contract);
                                }
                            }
                            else if (contract != null && contract.Status == ContractStatus.Active)
                            {
                                // Renovação de contrato ativo - estender validade por mais 1 ano
                                if (contract.ExpiryDate.HasValue)
                                {
                                    contract.ExpiryDate = contract.ExpiryDate.Value.AddYears(1);
                                }
                                else
                                {
                                    contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                                }
                                contract.UpdatedAt = DateTime.UtcNow;
                                _context.Contracts.Update(contract);
                            }
                        }
                    }

                    // Calcular e criar comissão (dentro da transação)
                    var commission = await _commissionService.CreateCommissionAsync(payment.Id);
                    if (commission != null)
                    {
                        _context.Commissions.Update(commission);
                    }

                    // Fazer commit de todas as alterações de uma vez
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return payment;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<Payment> UpdatePaymentStatusAsync(int paymentId, PaymentStatus status, string? transactionId = null)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");

            payment.Status = status;
            if (transactionId != null)
            {
                payment.TransactionId = transactionId;
            }
            if (status == PaymentStatus.Paid)
            {
                payment.PaidAt = DateTime.UtcNow;
            }

            return await _paymentRepository.UpdateAsync(payment);
        }

        public async Task<Payment> UpdatePaymentMethodAsync(int paymentId, PaymentMethod method)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");

            if (payment.Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Apenas pagamentos pendentes podem ter o método alterado");

            payment.Method = method;
            
            // Limpar códigos anteriores
            payment.PixCode = null;
            payment.PixExpiresAt = null;
            payment.BoletoCode = null;
            payment.BoletoBarcode = null;
            payment.BoletoExpiresAt = null;

            // Gerar novo código baseado no método
            if (method == PaymentMethod.PIX)
            {
                await GeneratePixCodeAsync(paymentId);
            }
            else if (method == PaymentMethod.BankTransfer)
            {
                await GenerateBoletoCodeAsync(paymentId);
            }

            return await _paymentRepository.UpdateAsync(payment);
        }

        public async Task<string> GeneratePixCodeAsync(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");

            // Buscar informações do cliente
            if (!payment.SubscriptionId.HasValue)
                throw new InvalidOperationException("Pagamento não possui assinatura associada.");
            var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
            if (subscription == null)
                throw new InvalidOperationException("Assinatura não encontrada");

            var contract = await _contractRepository.GetByIdAsync(subscription.ContractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");

            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == contract.ClientId);
            if (client == null)
                throw new InvalidOperationException("Cliente não encontrado");

            // Validar e preparar dados do cliente antes de enviar ao PagBank
            var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User.FullName;
            if (string.IsNullOrWhiteSpace(clientName))
            {
                throw new InvalidOperationException("Nome do cliente não informado. Complete seu cadastro em 'Meu Perfil' com Razão Social, Nome Fantasia ou Nome do Contato.");
            }

            var clientEmail = client.ContactEmail ?? client.User.Email;
            if (string.IsNullOrWhiteSpace(clientEmail) || !clientEmail.Contains("@"))
            {
                throw new InvalidOperationException("Email do cliente inválido. Complete seu cadastro em 'Meu Perfil' com um email válido.");
            }

            // Validar TaxId (CPF/CNPJ) - remover formatação e valores padrão inválidos
            var clientTaxId = client.CNPJ ?? client.CPF ?? "";
            if (!string.IsNullOrWhiteSpace(clientTaxId))
            {
                // Remover formatação
                clientTaxId = clientTaxId.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                
                // Verificar se não são valores padrão inválidos
                if (clientTaxId == "00000000000000" || clientTaxId == "00000000000" || 
                    clientTaxId == "99999999999" || clientTaxId == "99999999999999" ||
                    clientTaxId.All(c => c == '0') || clientTaxId.All(c => c == '9'))
                {
                    clientTaxId = "";
                }
            }

            if (string.IsNullOrWhiteSpace(clientTaxId) || clientTaxId.Length < 11)
            {
                throw new InvalidOperationException("CPF ou CNPJ não informado ou inválido. Complete seu cadastro em 'Meu Perfil' com um CPF ou CNPJ válido.");
            }

            var clientPhone = client.ContactPhone ?? client.User.Phone;

            var orderResponse = await _pagBankService.CreateOrderAsync(
                referenceId: payment.ExternalPaymentId,
                amount: payment.Amount,
                method: PaymentMethod.PIX,
                clientName: clientName,
                clientEmail: clientEmail,
                clientTaxId: clientTaxId,
                clientPhone: clientPhone
            );

            // Atualizar payment com dados do PagBank
            payment.ExternalPaymentId = orderResponse.Id;
            
            if (orderResponse.QrCodes != null && orderResponse.QrCodes.Any())
            {
                var qrCode = orderResponse.QrCodes.First();
                payment.PixCode = qrCode.Text;
                payment.PixExpiresAt = qrCode.ExpirationDate;
            }

            await _paymentRepository.UpdateAsync(payment);

            return payment.PixCode ?? "";
        }

        public async Task<string> GenerateBoletoCodeAsync(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");

            // Buscar informações do cliente
            if (!payment.SubscriptionId.HasValue)
                throw new InvalidOperationException("Pagamento não possui assinatura associada.");
            var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
            if (subscription == null)
                throw new InvalidOperationException("Assinatura não encontrada");

            var contract = await _contractRepository.GetByIdAsync(subscription.ContractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");

            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == contract.ClientId);
            if (client == null)
                throw new InvalidOperationException("Cliente não encontrado");

            // Validar e preparar dados do cliente antes de enviar ao PagBank
            var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User.FullName;
            if (string.IsNullOrWhiteSpace(clientName))
            {
                throw new InvalidOperationException("Nome do cliente não informado. Complete seu cadastro em 'Meu Perfil' com Razão Social, Nome Fantasia ou Nome do Contato.");
            }

            var clientEmail = client.ContactEmail ?? client.User.Email;
            if (string.IsNullOrWhiteSpace(clientEmail) || !clientEmail.Contains("@"))
            {
                throw new InvalidOperationException("Email do cliente inválido. Complete seu cadastro em 'Meu Perfil' com um email válido.");
            }

            // Validar TaxId (CPF/CNPJ) - remover formatação e valores padrão inválidos
            var clientTaxId = client.CNPJ ?? client.CPF ?? "";
            if (!string.IsNullOrWhiteSpace(clientTaxId))
            {
                // Remover formatação
                clientTaxId = clientTaxId.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                
                // Verificar se não são valores padrão inválidos
                if (clientTaxId == "00000000000000" || clientTaxId == "00000000000" || 
                    clientTaxId == "99999999999" || clientTaxId == "99999999999999" ||
                    clientTaxId.All(c => c == '0') || clientTaxId.All(c => c == '9'))
                {
                    clientTaxId = "";
                }
            }

            if (string.IsNullOrWhiteSpace(clientTaxId) || clientTaxId.Length < 11)
            {
                throw new InvalidOperationException("CPF ou CNPJ não informado ou inválido. Complete seu cadastro em 'Meu Perfil' com um CPF ou CNPJ válido.");
            }

            var clientPhone = client.ContactPhone ?? client.User.Phone;
            
            // Função auxiliar para validar e preencher campos
            string GetValueOrDefault(string? value, string defaultValue = "Não informado")
            {
                return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
            }
            
            // Preparar endereço do cliente para o boleto com validação completa
            // Street (Rua) - obrigatório
            var street = GetValueOrDefault(client.Address, "Não informado");
            
            // Number (Número) - obrigatório
            var number = "S/N"; // Padrão se não informado
            
            // Locality (Bairro) - obrigatório
            var locality = "Não informado";
            
            // City (Cidade) - obrigatório
            var city = GetValueOrDefault(client.City, "Não informado");
            
            // RegionCode (UF) - obrigatório, validar formato
            var validUFs = new[] { "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", 
                                  "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN", 
                                  "RS", "RO", "RR", "SC", "SP", "SE", "TO" };
            var regionCode = "SP"; // Padrão
            if (!string.IsNullOrWhiteSpace(client.State))
            {
                var stateTemp = client.State.Trim().ToUpper();
                if (stateTemp.Length >= 2)
                {
                    stateTemp = stateTemp.Substring(0, 2);
                    if (validUFs.Contains(stateTemp))
                    {
                        regionCode = stateTemp;
                    }
                }
            }
            
            // Country - obrigatório, sempre "BRA"
            var country = "BRA";
            
            // PostalCode (CEP) - obrigatório, validar formato (8 dígitos)
            var postalCode = "00000000"; // Padrão
            if (!string.IsNullOrWhiteSpace(client.ZipCode))
            {
                var postalCodeTemp = client.ZipCode
                    .Replace("-", "")
                    .Replace(".", "")
                    .Replace(" ", "")
                    .Trim();
                
                if (postalCodeTemp.Length == 8 && postalCodeTemp.All(char.IsDigit))
                {
                    postalCode = postalCodeTemp;
                }
                else if (postalCodeTemp.Length > 8)
                {
                    postalCode = postalCodeTemp.Substring(0, 8);
                }
                else if (postalCodeTemp.Length > 0 && postalCodeTemp.All(char.IsDigit))
                {
                    postalCode = postalCodeTemp.PadLeft(8, '0');
                }
            }
            
            var clientAddress = new PagBankAddress
            {
                Street = street,
                Number = number,
                Complement = null, // Opcional
                Locality = locality,
                City = city,
                RegionCode = regionCode,
                Country = country,
                PostalCode = postalCode
            };

            var orderResponse = await _pagBankService.CreateOrderAsync(
                referenceId: payment.ExternalPaymentId,
                amount: payment.Amount,
                method: PaymentMethod.BankTransfer,
                clientName: clientName,
                clientEmail: clientEmail,
                clientTaxId: clientTaxId,
                clientPhone: clientPhone,
                clientAddress: clientAddress
            );

            // Atualizar payment com dados do PagBank
            payment.ExternalPaymentId = orderResponse.Id;
            
            if (orderResponse.Charges != null && orderResponse.Charges.Any())
            {
                var charge = orderResponse.Charges.First();
                if (charge.PaymentMethod?.Boleto != null)
                {
                    payment.BoletoBarcode = charge.PaymentMethod.Boleto.Barcode;
                    payment.BoletoCode = charge.PaymentMethod.Boleto.FormattedBarcode;
                    payment.BoletoExpiresAt = charge.PaymentMethod.Boleto.DueDate;
                }
            }

            await _paymentRepository.UpdateAsync(payment);

            return payment.BoletoCode ?? payment.BoletoBarcode ?? "";
        }

        public async Task<Payment> ProcessPaymentWithCreditCardAsync(int paymentId, PagBankCard card, int? installments = null)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");

            if (payment.Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Apenas pagamentos pendentes podem ser processados");

            // Buscar informações do cliente
            if (!payment.SubscriptionId.HasValue)
                throw new InvalidOperationException("Pagamento não possui assinatura associada.");
            var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
            if (subscription == null)
                throw new InvalidOperationException("Assinatura não encontrada");

            var contract = await _contractRepository.GetByIdAsync(subscription.ContractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");

            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == contract.ClientId);
            if (client == null)
                throw new InvalidOperationException("Cliente não encontrado");

            // Validar e preparar dados do cliente antes de enviar ao PagBank
            var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User.FullName;
            if (string.IsNullOrWhiteSpace(clientName))
            {
                throw new InvalidOperationException("Nome do cliente não informado. Complete seu cadastro em 'Meu Perfil' com Razão Social, Nome Fantasia ou Nome do Contato.");
            }

            var clientEmail = client.ContactEmail ?? client.User.Email;
            if (string.IsNullOrWhiteSpace(clientEmail) || !clientEmail.Contains("@"))
            {
                throw new InvalidOperationException("Email do cliente inválido. Complete seu cadastro em 'Meu Perfil' com um email válido.");
            }

            // Validar TaxId (CPF/CNPJ) - remover formatação e valores padrão inválidos
            var clientTaxId = client.CNPJ ?? client.CPF ?? "";
            if (!string.IsNullOrWhiteSpace(clientTaxId))
            {
                // Remover formatação
                clientTaxId = clientTaxId.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
                
                // Verificar se não são valores padrão inválidos
                if (clientTaxId == "00000000000000" || clientTaxId == "00000000000" || 
                    clientTaxId == "99999999999" || clientTaxId == "99999999999999" ||
                    clientTaxId.All(c => c == '0') || clientTaxId.All(c => c == '9'))
                {
                    clientTaxId = "";
                }
            }

            if (string.IsNullOrWhiteSpace(clientTaxId) || clientTaxId.Length < 11)
            {
                throw new InvalidOperationException("CPF ou CNPJ não informado ou inválido. Complete seu cadastro em 'Meu Perfil' com um CPF ou CNPJ válido.");
            }

            var clientPhone = client.ContactPhone ?? client.User.Phone;

            // Aplicar percentual de cartão de crédito se configurado
            decimal amountToCharge = payment.Amount;
            var feePercentageStr = await _systemConfigurationService.GetValueAsync("Payment.CreditCardFeePercentage");
            if (!string.IsNullOrWhiteSpace(feePercentageStr) && decimal.TryParse(feePercentageStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fee) && fee > 0)
            {
                amountToCharge = payment.Amount * (1 + fee / 100);
            }

            var orderResponse = await _pagBankService.CreateOrderAsync(
                referenceId: payment.ExternalPaymentId,
                amount: amountToCharge,
                method: PaymentMethod.CreditCard,
                clientName: clientName,
                clientEmail: clientEmail,
                clientTaxId: clientTaxId,
                clientPhone: clientPhone,
                card: card,
                installments: installments
            );

            // Atualizar payment com dados do PagBank
            payment.ExternalPaymentId = orderResponse.Id;
            payment.Status = PaymentStatus.Processing;

            if (orderResponse.Charges != null && orderResponse.Charges.Any())
            {
                var charge = orderResponse.Charges.First();
                payment.Status = charge.Status.ToUpper() == "PAID" ? PaymentStatus.Paid : 
                                 charge.Status.ToUpper() == "DECLINED" ? PaymentStatus.Failed : 
                                 PaymentStatus.Processing;
                
                if (charge.PaidAt.HasValue)
                {
                    payment.PaidAt = charge.PaidAt.Value;
                }

                if (charge.PaymentMethod?.Card != null)
                {
                    payment.TransactionId = charge.Id;
                }
            }

            await _paymentRepository.UpdateAsync(payment);

            // Se o pagamento foi aprovado, confirmar
            if (payment.Status == PaymentStatus.Paid)
            {
                await ConfirmPaymentAsync(payment.Id, payment.TransactionId);
            }

            return payment;
        }

        public async Task<List<Payment>> GetPendingPaymentsAsync()
        {
            return await _paymentRepository.GetPendingPaymentsAsync();
        }

        public async Task<Payment> ConfirmPaymentAsync(int paymentId, string? transactionId = null)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var payment = await _paymentRepository.GetByIdAsync(paymentId);
                    if (payment == null)
                        throw new InvalidOperationException("Pagamento não encontrado");

                    if (payment.Status == PaymentStatus.Paid)
                    {
                        await transaction.CommitAsync();
                        return payment; // Já está pago
                    }

                    // Capturar valores antigos para auditoria
                    var oldStatus = payment.Status;
                    var oldPaidAt = payment.PaidAt;
                    var oldTransactionId = payment.TransactionId;

                    // Atualizar status para pago
                    payment.Status = PaymentStatus.Paid;
                    payment.PaidAt = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(transactionId))
                    {
                        payment.TransactionId = transactionId;
                    }
                    payment.UpdatedAt = DateTime.UtcNow;
                    _context.Payments.Update(payment);

                    // Log de auditoria: aprovação de pagamento
                    int? userId = null;
                    if (_httpContextAccessor?.HttpContext?.User != null)
                    {
                        var userIdClaim = _httpContextAccessor.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var parsedUserId))
                        {
                            userId = parsedUserId;
                        }
                    }

                    await LogAuditAsync(
                        userId,
                        "Pagamento aprovado",
                        "Payment",
                        payment.Id,
                        new { Status = oldStatus.ToString(), PaidAt = oldPaidAt, TransactionId = oldTransactionId },
                        new { Status = payment.Status.ToString(), PaidAt = payment.PaidAt, TransactionId = payment.TransactionId, Amount = payment.Amount }
                    );

                    // Atualizar assinatura
                    if (payment.SubscriptionId.HasValue)
                    {
                        var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId.Value);
                        if (subscription != null)
                        {
                            subscription.LastBillingDate = DateTime.UtcNow;
                            subscription.NextBillingDate = subscription.NextBillingDate.AddMonths(1);
                            subscription.FailedPaymentAttempts = 0;
                            if (subscription.Status == SubscriptionStatus.PastDue || subscription.Status == SubscriptionStatus.Suspended)
                            {
                                subscription.Status = SubscriptionStatus.Active;
                            }
                            subscription.UpdatedAt = DateTime.UtcNow;
                            _context.Subscriptions.Update(subscription);

                            // Ativar contrato se for o primeiro pagamento
                            var contract = await _context.Contracts
                                .Include(c => c.CoworkingAddress)
                                .FirstOrDefaultAsync(c => c.Id == subscription.ContractId);

                            if (contract != null && (contract.Status == ContractStatus.Suspended || contract.Status == ContractStatus.PendingPayment))
                            {
                                // Verificar se é o primeiro pagamento (não há pagamentos anteriores pagos)
                                var previousPaidPayments = await _context.Payments
                                    .Where(p => p.SubscriptionId == subscription.Id && 
                                               p.Id != paymentId && 
                                               p.Status == PaymentStatus.Paid)
                                    .CountAsync();

                                if (previousPaidPayments == 0)
                                {
                                    // Guardar o status original antes de alterar
                                    var originalStatus = contract.Status;
                                    
                                    // Primeiro pagamento - ativar contrato e definir validade de 1 ano
                                    contract.Status = ContractStatus.Active;
                                    contract.ExpiryDate = DateTime.UtcNow.AddYears(1); // Validade de 1 ano a partir do pagamento
                                    contract.UpdatedAt = DateTime.UtcNow;
                                    _context.Contracts.Update(contract);

                                    // Atualizar contador de contratos do endereço apenas se estava aguardando pagamento
                                    // (contratos suspensos já foram contados anteriormente)
                                    if (contract.CoworkingAddress != null && originalStatus == ContractStatus.PendingPayment)
                                    {
                                        contract.CoworkingAddress.CurrentContracts++;
                                        if (contract.CoworkingAddress.CurrentContracts >= contract.CoworkingAddress.MaxContracts)
                                        {
                                            contract.CoworkingAddress.IsAvailable = false;
                                        }
                                        _context.CoworkingAddresses.Update(contract.CoworkingAddress);
                                    }

                                    // Gerar declaração automaticamente após o primeiro pagamento
                                    if (_addressDeclarationService != null && contract.ExpiryDate.HasValue)
                                    {
                                        try
                                        {
                                            await _addressDeclarationService.GenerateDeclarationAsync(contract.Id);
                                        }
                                        catch (Exception ex)
                                        {
                                            // Log do erro mas não impede a confirmação do pagamento
                                            System.Diagnostics.Debug.WriteLine($"Erro ao gerar declaração automaticamente: {ex.Message}");
                                        }
                                    }
                                }
                                else
                                {
                                    // Renovação - estender validade por mais 1 ano
                                    if (contract.ExpiryDate.HasValue)
                                    {
                                        contract.ExpiryDate = contract.ExpiryDate.Value.AddYears(1);
                                    }
                                    else
                                    {
                                        contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                                    }
                                    contract.UpdatedAt = DateTime.UtcNow;
                                    _context.Contracts.Update(contract);
                                }
                            }
                            else if (contract != null && contract.Status == ContractStatus.Active)
                            {
                                // Renovação de contrato ativo - estender validade por mais 1 ano
                                if (contract.ExpiryDate.HasValue)
                                {
                                    contract.ExpiryDate = contract.ExpiryDate.Value.AddYears(1);
                                }
                                else
                                {
                                    contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                                }
                                contract.UpdatedAt = DateTime.UtcNow;
                                _context.Contracts.Update(contract);
                            }
                        }
                    }

                    // Calcular e criar comissão (dentro da transação)
                    var commission = await _commissionService.CreateCommissionAsync(payment.Id);
                    if (commission != null)
                    {
                        _context.Commissions.Update(commission);
                    }

                    // Fazer commit de todas as alterações de uma vez
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return payment;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<List<Payment>> GetPaymentsByClientIdAsync(int clientId)
        {
            return await _context.Payments
                .Include(p => p.Subscription)
                    .ThenInclude(s => s.Contract)
                .Where(p => p.Subscription.Contract.ClientId == clientId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<Payment?> GetPaymentByIdAsync(int paymentId)
        {
            return await _paymentRepository.GetByIdAsync(paymentId);
        }

        private string GenerateCRC16(string data)
        {
            // Implementação simplificada de CRC16
            ushort crc = 0xFFFF;
            foreach (char c in data)
            {
                crc ^= (ushort)(c << 8);
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc <<= 1;
                }
            }
            return (crc ^ 0xFFFF).ToString("X4");
        }
    }
}
