using KlyonEF.Models;
using KlyonEF.Repositories;
using KlyonEF.Data;
using Microsoft.EntityFrameworkCore;

namespace KlyonEF.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly ISubscriptionRepository _subscriptionRepository;
        private readonly ICommissionService _commissionService;
        private readonly ApplicationDbContext _context;

        public PaymentService(
            IPaymentRepository paymentRepository,
            ISubscriptionRepository subscriptionRepository,
            ICommissionService commissionService,
            ApplicationDbContext context)
        {
            _paymentRepository = paymentRepository;
            _subscriptionRepository = subscriptionRepository;
            _commissionService = commissionService;
            _context = context;
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

        public async Task<string> GeneratePixCodeAsync(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");

            // TODO: Integrar com gerador de PIX real
            var pixCode = $"00020126580014BR.GOV.BCB.PIX0136{payment.ExternalPaymentId}5204000053039865802BR5925KLYON ENDERECO FISCAL6009SAO PAULO62070503***6304{GenerateCRC16($"00020126580014BR.GOV.BCB.PIX0136{payment.ExternalPaymentId}5204000053039865802BR5925KLYON ENDERECO FISCAL6009SAO PAULO62070503***")}";

            payment.PixCode = pixCode;
            await _paymentRepository.UpdateAsync(payment);

            return pixCode;
        }

        public async Task<List<Payment>> GetPendingPaymentsAsync()
        {
            return await _paymentRepository.GetPendingPaymentsAsync();
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
