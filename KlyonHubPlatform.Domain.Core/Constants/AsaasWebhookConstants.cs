namespace KlyonHubPlatform.Domain.Core.Constants
{
    public static class AsaasWebhookEvents
    {
        public const string PaymentReceived = "PAYMENT_RECEIVED";
        public const string PaymentConfirmed = "PAYMENT_CONFIRMED";
        public const string PaymentReceivedInCash = "PAYMENT_RECEIVED_IN_CASH";
        public const string PaymentDeleted = "PAYMENT_DELETED";
        public const string PaymentOverdue = "PAYMENT_OVERDUE";
        public const string PixAutomaticAuthorizationActivated = "PIX_AUTOMATIC_RECURRING_AUTHORIZATION_ACTIVATED";
        public const string PixAutomaticPaymentInstructionRefused = "PIX_AUTOMATIC_RECURRING_PAYMENT_INSTRUCTION_REFUSED";
    }
    public static class AsaasPaymentStatus
    {
        public const string Received = "RECEIVED";
        public const string Confirmed = "CONFIRMED";
    }
    public static class AsaasWebhookPayloadKeys
    {
        public const string Event = "event";
        public const string Payment = "payment";
        public const string Id = "id";
        public const string Status = "status";
    }
}