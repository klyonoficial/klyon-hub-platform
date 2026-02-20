namespace KlyonHubPlatform.Domain.Core.Enums
{
    public enum PagBankWebhookEvent
    {
        Unknown = 0,
        PaymentPaid,
        ChargePaid,
        OrderPaid,
        PaymentCanceled,
        ChargeCanceled,
        OrderCanceled,
        PaymentRefunded,
        ChargeRefunded,
        OrderRefunded,
        PaymentInAnalysis,
        ChargeInAnalysis,
        PaymentDeclined,
        ChargeDeclined,
        PaymentWaiting,
        ChargeWaiting
    }
    public static class PagBankWebhookEventExtensions
    {
        public static PagBankWebhookEvent ToWebhookEvent(this string eventName)
        {
            if (string.IsNullOrEmpty(eventName))
                return PagBankWebhookEvent.Unknown;
            return eventName.ToUpper() switch
            {
                "PAYMENT_PAID" => PagBankWebhookEvent.PaymentPaid,
                "CHARGE_PAID" => PagBankWebhookEvent.ChargePaid,
                "ORDER_PAID" => PagBankWebhookEvent.OrderPaid,
                "PAYMENT_CANCELED" => PagBankWebhookEvent.PaymentCanceled,
                "CHARGE_CANCELED" => PagBankWebhookEvent.ChargeCanceled,
                "ORDER_CANCELED" => PagBankWebhookEvent.OrderCanceled,
                "PAYMENT_REFUNDED" => PagBankWebhookEvent.PaymentRefunded,
                "CHARGE_REFUNDED" => PagBankWebhookEvent.ChargeRefunded,
                "ORDER_REFUNDED" => PagBankWebhookEvent.OrderRefunded,
                "PAYMENT_IN_ANALYSIS" => PagBankWebhookEvent.PaymentInAnalysis,
                "CHARGE_IN_ANALYSIS" => PagBankWebhookEvent.ChargeInAnalysis,
                "PAYMENT_DECLINED" => PagBankWebhookEvent.PaymentDeclined,
                "CHARGE_DECLINED" => PagBankWebhookEvent.ChargeDeclined,
                "PAYMENT_WAITING" => PagBankWebhookEvent.PaymentWaiting,
                "CHARGE_WAITING" => PagBankWebhookEvent.ChargeWaiting,
                _ => PagBankWebhookEvent.Unknown
            };
        }
        public static bool IsPaidEvent(this PagBankWebhookEvent evt) =>
            evt is PagBankWebhookEvent.PaymentPaid 
                or PagBankWebhookEvent.ChargePaid 
                or PagBankWebhookEvent.OrderPaid;
        public static bool IsCanceledEvent(this PagBankWebhookEvent evt) =>
            evt is PagBankWebhookEvent.PaymentCanceled 
                or PagBankWebhookEvent.ChargeCanceled 
                or PagBankWebhookEvent.OrderCanceled;
        public static bool IsRefundedEvent(this PagBankWebhookEvent evt) =>
            evt is PagBankWebhookEvent.PaymentRefunded 
                or PagBankWebhookEvent.ChargeRefunded 
                or PagBankWebhookEvent.OrderRefunded;
        public static bool IsInAnalysisEvent(this PagBankWebhookEvent evt) =>
            evt is PagBankWebhookEvent.PaymentInAnalysis 
                or PagBankWebhookEvent.ChargeInAnalysis;
        public static bool IsDeclinedEvent(this PagBankWebhookEvent evt) =>
            evt is PagBankWebhookEvent.PaymentDeclined 
                or PagBankWebhookEvent.ChargeDeclined;
        public static bool IsWaitingEvent(this PagBankWebhookEvent evt) =>
            evt is PagBankWebhookEvent.PaymentWaiting 
                or PagBankWebhookEvent.ChargeWaiting;
    }
}