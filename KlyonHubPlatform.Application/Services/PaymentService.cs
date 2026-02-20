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
using System.Security.Claims;

namespace KlyonHubPlatform.Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly ICommissionService _commissionService;
        private readonly IContractRepository _contractRepository;
        private readonly ICoworkingRepository _coworkingRepository;
        private readonly IAsaasService _asaasService;
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
            IAsaasService asaasService,
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
            _asaasService = asaasService;
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
                userAgent = _httpContextAccessor.HttpContext.Request.Headers[HttpHeaderNames.UserAgent].ToString();
            }

            string? oldValuesJson = oldValues != null ? JsonSerializer.Serialize(oldValues) : null;
            string? newValuesJson = newValues != null ? JsonSerializer.Serialize(newValues) : null;

            await _auditService.LogActionAsync(userId, action, entityType, entityId, oldValuesJson, newValuesJson, ipAddress, userAgent);
        }

        /// <summary>
        /// Cancela todos os outros contratos do cliente que estão com pagamento pendente,
        /// pois o cliente só pode possuir um endereço fiscal ativo.
        /// Também cancela os pagamentos (PaymentStatus.Cancelled) e registra histórico com motivo claro.
        /// </summary>
        private async Task CancelOtherPendingPaymentContractsForClientAsync(int clientId, int activeContractId)
        {
            var activeContract = await _context.Contracts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == activeContractId);
            var activeContractNumber = activeContract?.ContractNumber ?? "N/A";
            var cancellationReasonContract = "Cliente já possui um endereço fiscal ativo. Outro contrato " + activeContractNumber + " foi aprovado o pagamento e ativado.";
            var rejectionReasonPayment = "Cancelado pois outro contrato " + activeContractNumber + " foi aprovado o pagamento e ativado.";

            var others = await _context.Contracts
                .Include(c => c.Subscription)
                .Where(c => c.ClientId == clientId && c.Id != activeContractId && c.Status == ContractStatus.PendingPayment)
                .ToListAsync();
            var now = DateTime.UtcNow;
            var subscriptionIds = new List<int>();
            foreach (var c in others)
            {
                c.Status = ContractStatus.Cancelled;
                c.CancelledAt = now;
                c.CancellationReason = cancellationReasonContract;
                c.UpdatedAt = now;
                _context.Contracts.Update(c);
                if (c.Subscription != null)
                    subscriptionIds.Add(c.Subscription.Id);
            }
            if (subscriptionIds.Count > 0)
            {
                var paymentsToCancel = await _context.Payments
                    .Where(p => p.SubscriptionId != null && subscriptionIds.Contains(p.SubscriptionId.Value)
                        && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing || p.Status == PaymentStatus.AwaitingValidation))
                    .ToListAsync();
                var actionBy = 0;
                var userIdClaim = _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var uid))
                    actionBy = uid;
                foreach (var p in paymentsToCancel)
                {
                    p.Status = PaymentStatus.Cancelled;
                    p.UpdatedAt = now;
                    _context.Payments.Update(p);
                    if (actionBy > 0)
                    {
                        _context.PaymentHistories.Add(new PaymentHistory
                        {
                            PaymentId = p.Id,
                            Action = PaymentHistoryActions.Cancelled,
                            RejectionReason = rejectionReasonPayment,
                            ActionBy = actionBy,
                            ActionAt = now
                        });
                    }
                }
            }
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

                                    // Cancelar outros contratos pendentes do cliente (só pode ter um endereço fiscal ativo)
                                    await CancelOtherPendingPaymentContractsForClientAsync(contract.ClientId, contract.Id);
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
                if (BrazilianTaxIdPlaceholders.IsPlaceholder(clientTaxId) ||
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

            var validation = _asaasService.ValidateCustomerDataForPayment(clientName, clientEmail, clientTaxId, clientPhone, payment.Amount);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.Message);

            var orderResponse = await _asaasService.CreateOrderAsync(
                referenceId: payment.ExternalPaymentId,
                amount: payment.Amount,
                method: PaymentMethod.PIX,
                clientName: clientName,
                clientEmail: clientEmail,
                clientTaxId: clientTaxId,
                clientPhone: clientPhone
            );

            // Atualizar payment com dados do Asaas
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
                if (BrazilianTaxIdPlaceholders.IsPlaceholder(clientTaxId) ||
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
            string GetValueOrDefault(string? value, string defaultValue = DefaultDisplayStrings.NaoInformado)
            {
                return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
            }
            
            // Preparar endereço do cliente para o boleto com validação completa
            // Street (Rua) - obrigatório
            var street = GetValueOrDefault(client.Address, DefaultDisplayStrings.NaoInformado);
            
            // Number (Número) - obrigatório
            var number = "S/N"; // Padrão se não informado
            
            // Locality (Bairro) - obrigatório
            var locality = DefaultDisplayStrings.NaoInformado;
            
            // City (Cidade) - obrigatório
            var city = GetValueOrDefault(client.City, DefaultDisplayStrings.NaoInformado);
            
            // RegionCode (UF) - obrigatório, validar formato
            var regionCode = "SP"; // Padrão
            if (!string.IsNullOrWhiteSpace(client.State))
            {
                var stateTemp = client.State.Trim().ToUpper();
                if (stateTemp.Length >= 2)
                {
                    stateTemp = stateTemp.Substring(0, 2);
                    if (BrazilianUfs.All.Contains(stateTemp))
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

            var orderResponse = await _asaasService.CreateOrderAsync(
                referenceId: payment.ExternalPaymentId,
                amount: payment.Amount,
                method: PaymentMethod.BankTransfer,
                clientName: clientName,
                clientEmail: clientEmail,
                clientTaxId: clientTaxId,
                clientPhone: clientPhone,
                clientAddress: clientAddress
            );

            // Atualizar payment com dados do Asaas
            payment.ExternalPaymentId = orderResponse.Id;
            
            if (orderResponse.Charges != null && orderResponse.Charges.Any())
            {
                var charge = orderResponse.Charges.First();
                if (charge.PaymentMethod?.Boleto != null)
                {
                    payment.BoletoBarcode = charge.PaymentMethod.Boleto.Barcode;
                    payment.BoletoCode = charge.PaymentMethod.Boleto.FormattedBarcode ?? charge.PaymentMethod.Boleto.Barcode;
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
                if (BrazilianTaxIdPlaceholders.IsPlaceholder(clientTaxId) ||
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

            // Endereço do cliente (cadastrado no perfil) - obrigatório para o Asaas (CEP do titular do cartão)
            string GetValueOrDefault(string? value, string defaultValue = DefaultDisplayStrings.NaoInformado)
            {
                return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
            }
            var postalCode = "00000000";
            if (!string.IsNullOrWhiteSpace(client.ZipCode))
            {
                var postalCodeTemp = client.ZipCode.Replace("-", "").Replace(".", "").Replace(" ", "").Trim();
                if (postalCodeTemp.Length == 8 && postalCodeTemp.All(char.IsDigit))
                    postalCode = postalCodeTemp;
                else if (postalCodeTemp.Length > 8)
                    postalCode = postalCodeTemp.Substring(0, 8);
                else if (postalCodeTemp.Length > 0 && postalCodeTemp.All(char.IsDigit))
                    postalCode = postalCodeTemp.PadLeft(8, '0');
            }
            if (postalCode == "00000000" || string.IsNullOrWhiteSpace(client.ZipCode))
            {
                throw new InvalidOperationException("CEP não informado. Atualize em 'Meu Perfil' com o CEP do titular do cartão.");
            }
            var regionCode = "SP";
            if (!string.IsNullOrWhiteSpace(client.State))
            {
                var stateTemp = client.State.Trim().ToUpper();
                if (stateTemp.Length >= 2 && BrazilianUfs.All.Contains(stateTemp.Substring(0, 2)))
                    regionCode = stateTemp.Substring(0, 2);
            }
            var clientAddress = new PagBankAddress
            {
                Street = GetValueOrDefault(client.Address, DefaultDisplayStrings.NaoInformado),
                Number = "S/N",
                Complement = null,
                Locality = DefaultDisplayStrings.NaoInformado,
                City = GetValueOrDefault(client.City, DefaultDisplayStrings.NaoInformado),
                RegionCode = regionCode,
                Country = "BRA",
                PostalCode = postalCode
            };

            // Aplicar percentual de cartão de crédito se configurado
            decimal amountToCharge = payment.Amount;
            var feePercentageStr = await _systemConfigurationService.GetValueAsync(PaymentConfigKey.CreditCardFeePercentage.ToConfigKey());
            if (!string.IsNullOrWhiteSpace(feePercentageStr) && decimal.TryParse(feePercentageStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var fee) && fee > 0)
            {
                amountToCharge = payment.Amount * (1 + fee / 100);
            }

            var orderResponse = await _asaasService.CreateOrderAsync(
                referenceId: payment.ExternalPaymentId,
                amount: amountToCharge,
                method: PaymentMethod.CreditCard,
                clientName: clientName,
                clientEmail: clientEmail,
                clientTaxId: clientTaxId,
                clientPhone: clientPhone,
                card: card,
                installments: installments,
                clientAddress: clientAddress
            );

            // Atualizar payment com dados do Asaas
            payment.ExternalPaymentId = orderResponse.Id;
            payment.Status = PaymentStatus.Processing;
            var installmentCount = installments ?? 1;
            payment.InstallmentCount = installmentCount;
            payment.InstallmentValue = Math.Round(amountToCharge / installmentCount, 2);

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

                    // Registrar histórico de validação para a coluna "Validação" mostrar "Aprovado" (manual) ou "Aprovado automaticamente" (PIX/webhook)
                    _context.PaymentHistories.Add(new PaymentHistory
                    {
                        PaymentId = payment.Id,
                        Action = PaymentHistoryActions.Approved,
                        ActionBy = userId,
                        ActionAt = DateTime.UtcNow
                    });

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

                                    // Cancelar outros contratos pendentes do cliente (só pode ter um endereço fiscal ativo)
                                    await CancelOtherPendingPaymentContractsForClientAsync(contract.ClientId, contract.Id);
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
                    .ThenInclude(s => s!.Contract)
                .Where(p => p.Subscription != null && p.Subscription.Contract != null && p.Subscription.Contract.ClientId == clientId)
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
