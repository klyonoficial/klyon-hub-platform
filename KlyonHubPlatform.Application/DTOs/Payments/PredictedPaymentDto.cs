using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.DTOs.Payments
{
    /// <summary>Próxima parcela prevista quando o Asaas ainda não emitiu cobrança real.</summary>
    public class PredictedPaymentDto
    {
        public int SubscriptionId { get; set; }
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentMethod Method { get; set; }
        public DateTime DueDate { get; set; }
        public string StatusLabel { get; set; } = "Previsto";
        public string Explanation { get; set; } = string.Empty;
    }
}
