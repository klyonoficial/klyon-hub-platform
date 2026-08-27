using KlyonHubPlatform.Domain.Core.Enums;
namespace KlyonHubPlatform.Domain.Entities
{
    public class Payment
    {
        public int Id { get; set; }
        public int? SubscriptionId { get; set; }
        public int? ReservationId { get; set; }
        /// <summary>PIX de adesão do fluxo CNPJ primeiro (sem assinatura/contrato ainda).</summary>
        public int? FiscalOnboardingLeadId { get; set; }
        public string ExternalPaymentId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int? PromoCodeId { get; set; }
        public string? PromoCodeSnapshot { get; set; }
        public decimal SnapshotDiscountAmount { get; set; }
        public decimal SnapshotListPrice { get; set; }
        /// <summary>Ciclo de cobrança no momento da emissão da parcela (Monthly/Yearly). Imutável após criação.</summary>
        public string? SnapshotBillingCycle { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public PaymentMethod Method { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaidAt { get; set; }
        public string? TransactionId { get; set; }
        public string? PixCode { get; set; }
        /// <summary>Identificador de conciliação do QR PIX (ex.: autorização PIX Automático). Usado para vincular PAYMENT_RECEIVED quando externalReference vem null.</summary>
        public string? PixConciliationIdentifier { get; set; }
        public DateTime? PixExpiresAt { get; set; }
        public string? BoletoCode { get; set; }
        public string? BoletoBarcode { get; set; }
        /// <summary>URL do boleto em PDF retornada pelo Asaas (bankSlipUrl). Permite ao usuário abrir/gerar o boleto.</summary>
        public string? BankSlipUrl { get; set; }
        public DateTime? BoletoExpiresAt { get; set; }
        public string? FailureReason { get; set; }
        public string? ReceiptFilePath { get; set; }
        public string? ValidationNotes { get; set; }
        public int? InstallmentCount { get; set; }
        public decimal? InstallmentValue { get; set; }
        public int? ValidatedBy { get; set; }
        public DateTime? ValidatedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Subscription? Subscription { get; set; }
        public virtual Reservation? Reservation { get; set; }
        public virtual FiscalOnboardingLead? FiscalOnboardingLead { get; set; }
        public virtual User? Validator { get; set; }
        public virtual PromoCode? PromoCode { get; set; }
    }
}