namespace KlyonHubPlatform.Application.DTOs.Payments
{
    /// <summary>Resultado do processamento centralizado de webhooks Asaas.</summary>
    public sealed class AsaasWebhookHandleResult
    {
        public bool Handled { get; init; }
        public int? PaymentIdForCancelledNotification { get; init; }
        public int? PaymentIdForPixRegeneratedNotification { get; init; }

        public static AsaasWebhookHandleResult NotHandled() => new() { Handled = false };

        public static AsaasWebhookHandleResult Done(
            int? paymentIdForCancelledNotification = null,
            int? paymentIdForPixRegeneratedNotification = null) =>
            new()
            {
                Handled = true,
                PaymentIdForCancelledNotification = paymentIdForCancelledNotification,
                PaymentIdForPixRegeneratedNotification = paymentIdForPixRegeneratedNotification
            };
    }
}
