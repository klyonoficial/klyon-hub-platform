namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Eventos de webhook do PagBank
    /// </summary>
    public enum PagBankWebhookEvent
    {
        /// <summary>
        /// Evento desconhecido ou não mapeado
        /// </summary>
        Unknown = 0,

        // Eventos de pagamento confirmado
        PaymentPaid,
        ChargePaid,
        OrderPaid,

        // Eventos de pagamento cancelado
        PaymentCanceled,
        ChargeCanceled,
        OrderCanceled,

        // Eventos de estorno
        PaymentRefunded,
        ChargeRefunded,
        OrderRefunded,

        // Eventos de análise
        PaymentInAnalysis,
        ChargeInAnalysis,

        // Eventos de recusa
        PaymentDeclined,
        ChargeDeclined,

        // Eventos de aguardando pagamento
        PaymentWaiting,
        ChargeWaiting
    }

    /// <summary>
    /// Extensões para conversão de strings para enum
    /// </summary>
    public static class PagBankWebhookEventExtensions
    {
        /// <summary>
        /// Converte string do evento para enum
        /// </summary>
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

        /// <summary>
        /// Verifica se é um evento de pagamento confirmado
        /// </summary>
        public static bool IsPaidEvent(this PagBankWebhookEvent evt) =>
            evt is PagBankWebhookEvent.PaymentPaid 
                or PagBankWebhookEvent.ChargePaid 
                or PagBankWebhookEvent.OrderPaid;

        /// <summary>
        /// Verifica se é um evento de cancelamento
        /// </summary>
        public static bool IsCanceledEvent(this PagBankWebhookEvent evt) =>
            evt is PagBankWebhookEvent.PaymentCanceled 
                or PagBankWebhookEvent.ChargeCanceled 
                or PagBankWebhookEvent.OrderCanceled;

        /// <summary>
        /// Verifica se é um evento de estorno
        /// </summary>
        public static bool IsRefundedEvent(this PagBankWebhookEvent evt) =>
            evt is PagBankWebhookEvent.PaymentRefunded 
                or PagBankWebhookEvent.ChargeRefunded 
                or PagBankWebhookEvent.OrderRefunded;

        /// <summary>
        /// Verifica se é um evento de análise
        /// </summary>
        public static bool IsInAnalysisEvent(this PagBankWebhookEvent evt) =>
            evt is PagBankWebhookEvent.PaymentInAnalysis 
                or PagBankWebhookEvent.ChargeInAnalysis;

        /// <summary>
        /// Verifica se é um evento de recusa
        /// </summary>
        public static bool IsDeclinedEvent(this PagBankWebhookEvent evt) =>
            evt is PagBankWebhookEvent.PaymentDeclined 
                or PagBankWebhookEvent.ChargeDeclined;

        /// <summary>
        /// Verifica se é um evento de aguardando
        /// </summary>
        public static bool IsWaitingEvent(this PagBankWebhookEvent evt) =>
            evt is PagBankWebhookEvent.PaymentWaiting 
                or PagBankWebhookEvent.ChargeWaiting;
    }
}
