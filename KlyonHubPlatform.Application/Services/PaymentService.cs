using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Repositories;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using KlyonHubPlatform.Domain.Interfaces;

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

        public PaymentService(
            IPaymentRepository paymentRepository,
            ISubscriptionRepository subscriptionRepository,
            ICommissionService commissionService,
            IContractRepository contractRepository,
            ICoworkingRepository coworkingRepository,
            IPagBankService pagBankService,
            ApplicationDbContext context,
            IAddressDeclarationService? addressDeclarationService = null)
        {
            _paymentRepository = paymentRepository;
            _subscriptionRepository = subscriptionRepository;
            _commissionService = commissionService;
            _contractRepository = contractRepository;
            _coworkingRepository = coworkingRepository;
            _pagBankService = pagBankService;
            _context = context;
            _addressDeclarationService = addressDeclarationService;
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
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");

            // TODO: Integrar com gateway de pagamento real (Stripe, PagSeguro, etc.)
            // Por enquanto, simula processamento
            payment.Status = PaymentStatus.Processing;
            await _paymentRepository.UpdateAsync(payment);

            // Simular processamento assíncrono
            await Task.Delay(1000);

            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = DateTime.UtcNow;
            payment.TransactionId = Guid.NewGuid().ToString();
            await _paymentRepository.UpdateAsync(payment);

            // Atualizar assinatura
            var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId);
            if (subscription != null)
            {
                subscription.LastBillingDate = DateTime.UtcNow;
                subscription.NextBillingDate = subscription.NextBillingDate.AddMonths(1);
                subscription.FailedPaymentAttempts = 0;
                if (subscription.Status == SubscriptionStatus.PastDue)
                {
                    subscription.Status = SubscriptionStatus.Active;
                }
                await _subscriptionRepository.UpdateAsync(subscription);

                // Ativar contrato se for o primeiro pagamento
                var contract = await _context.Contracts
                    .Include(c => c.CoworkingAddress)
                    .FirstOrDefaultAsync(c => c.Id == subscription.ContractId);

                if (contract != null && contract.Status == ContractStatus.Suspended)
                {
                    // Verificar se é o primeiro pagamento (não há pagamentos anteriores pagos)
                    var previousPaidPayments = await _context.Payments
                        .Where(p => p.SubscriptionId == subscription.Id && 
                                   p.Id != paymentId && 
                                   p.Status == PaymentStatus.Paid)
                        .CountAsync();

                    if (previousPaidPayments == 0)
                    {
                        // Primeiro pagamento - ativar contrato e definir validade de 1 ano
                        contract.Status = ContractStatus.Active;
                        contract.ExpiryDate = DateTime.UtcNow.AddYears(1); // Validade de 1 ano a partir do pagamento
                        await _contractRepository.UpdateAsync(contract);

                        // Atualizar contador de contratos do endereço
                        if (contract.CoworkingAddress != null)
                        {
                            contract.CoworkingAddress.CurrentContracts++;
                            if (contract.CoworkingAddress.CurrentContracts >= contract.CoworkingAddress.MaxContracts)
                            {
                                contract.CoworkingAddress.IsAvailable = false;
                            }
                            await _coworkingRepository.UpdateAddressAsync(contract.CoworkingAddress);
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
                        await _contractRepository.UpdateAsync(contract);
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
                    await _contractRepository.UpdateAsync(contract);
                }
            }

            // Calcular e criar comissão
            await _commissionService.CreateCommissionAsync(payment.Id);

            return payment;
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
            var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId);
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

            // Criar pedido no PagBank
            var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User.FullName;
            var clientEmail = client.ContactEmail ?? client.User.Email;
            var clientTaxId = client.CNPJ ?? client.CPF ?? "";
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
            var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId);
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

            // Criar pedido no PagBank
            var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User.FullName;
            var clientEmail = client.ContactEmail ?? client.User.Email;
            var clientTaxId = client.CNPJ ?? client.CPF ?? "";
            var clientPhone = client.ContactPhone ?? client.User.Phone;

            var orderResponse = await _pagBankService.CreateOrderAsync(
                referenceId: payment.ExternalPaymentId,
                amount: payment.Amount,
                method: PaymentMethod.BankTransfer,
                clientName: clientName,
                clientEmail: clientEmail,
                clientTaxId: clientTaxId,
                clientPhone: clientPhone
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
            var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId);
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

            // Criar pedido no PagBank com cartão
            var clientName = client.RazaoSocial ?? client.NomeFantasia ?? client.ContactName ?? client.User.FullName;
            var clientEmail = client.ContactEmail ?? client.User.Email;
            var clientTaxId = client.CNPJ ?? client.CPF ?? "";
            var clientPhone = client.ContactPhone ?? client.User.Phone;

            var orderResponse = await _pagBankService.CreateOrderAsync(
                referenceId: payment.ExternalPaymentId,
                amount: payment.Amount,
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
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");

            if (payment.Status == PaymentStatus.Paid)
                return payment; // Já está pago

            // Atualizar status para pago
            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(transactionId))
            {
                payment.TransactionId = transactionId;
            }
            await _paymentRepository.UpdateAsync(payment);

            // Atualizar assinatura
            var subscription = await _subscriptionRepository.GetByIdAsync(payment.SubscriptionId);
            if (subscription != null)
            {
                subscription.LastBillingDate = DateTime.UtcNow;
                subscription.NextBillingDate = subscription.NextBillingDate.AddMonths(1);
                subscription.FailedPaymentAttempts = 0;
                if (subscription.Status == SubscriptionStatus.PastDue || subscription.Status == SubscriptionStatus.Suspended)
                {
                    subscription.Status = SubscriptionStatus.Active;
                }
                await _subscriptionRepository.UpdateAsync(subscription);

                // Ativar contrato se for o primeiro pagamento
                var contract = await _context.Contracts
                    .Include(c => c.CoworkingAddress)
                    .FirstOrDefaultAsync(c => c.Id == subscription.ContractId);

                if (contract != null && contract.Status == ContractStatus.Suspended)
                {
                    // Verificar se é o primeiro pagamento (não há pagamentos anteriores pagos)
                    var previousPaidPayments = await _context.Payments
                        .Where(p => p.SubscriptionId == subscription.Id && 
                                   p.Id != paymentId && 
                                   p.Status == PaymentStatus.Paid)
                        .CountAsync();

                    if (previousPaidPayments == 0)
                    {
                        // Primeiro pagamento - ativar contrato e definir validade de 1 ano
                        contract.Status = ContractStatus.Active;
                        contract.ExpiryDate = DateTime.UtcNow.AddYears(1); // Validade de 1 ano a partir do pagamento
                        await _contractRepository.UpdateAsync(contract);

                        // Atualizar contador de contratos do endereço
                        if (contract.CoworkingAddress != null)
                        {
                            contract.CoworkingAddress.CurrentContracts++;
                            if (contract.CoworkingAddress.CurrentContracts >= contract.CoworkingAddress.MaxContracts)
                            {
                                contract.CoworkingAddress.IsAvailable = false;
                            }
                            await _coworkingRepository.UpdateAddressAsync(contract.CoworkingAddress);
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
                        await _contractRepository.UpdateAsync(contract);
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
                    await _contractRepository.UpdateAsync(contract);
                }
            }

            // Calcular e criar comissão
            await _commissionService.CreateCommissionAsync(payment.Id);

            return payment;
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
