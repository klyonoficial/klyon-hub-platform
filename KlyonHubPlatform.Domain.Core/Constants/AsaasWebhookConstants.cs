namespace KlyonHubPlatform.Domain.Core.Constants
{
    public static class AsaasWebhookEvents
    {
        public const string PaymentReceived = "PAYMENT_RECEIVED";
        public const string PaymentConfirmed = "PAYMENT_CONFIRMED";
        public const string PaymentReceivedInCash = "PAYMENT_RECEIVED_IN_CASH";
        public const string PaymentDeleted = "PAYMENT_DELETED";
        public const string PaymentOverdue = "PAYMENT_OVERDUE";
        public const string PaymentCreated = "PAYMENT_CREATED";
        public const string PaymentUpdated = "PAYMENT_UPDATED";
        public const string PaymentRejected = "PAYMENT_REJECTED";
        public const string PaymentCreditCardCaptureRefused = "PAYMENT_CREDIT_CARD_CAPTURE_REFUSED";
        public const string PaymentRefunded = "PAYMENT_REFUNDED";
        public const string PaymentRestored = "PAYMENT_RESTORED";
        public const string PixAutomaticAuthorizationCreated = "PIX_AUTOMATIC_RECURRING_AUTHORIZATION_CREATED";
        public const string PixAutomaticAuthorizationActivated = "PIX_AUTOMATIC_RECURRING_AUTHORIZATION_ACTIVATED";
        public const string PixAutomaticAuthorizationCancelled = "PIX_AUTOMATIC_RECURRING_AUTHORIZATION_CANCELLED";
        public const string PixAutomaticAuthorizationExpired = "PIX_AUTOMATIC_RECURRING_AUTHORIZATION_EXPIRED";
        public const string PixAutomaticAuthorizationRefused = "PIX_AUTOMATIC_RECURRING_AUTHORIZATION_REFUSED";
        public const string PixAutomaticPaymentInstructionCreated = "PIX_AUTOMATIC_RECURRING_PAYMENT_INSTRUCTION_CREATED";
        public const string PixAutomaticPaymentInstructionScheduled = "PIX_AUTOMATIC_RECURRING_PAYMENT_INSTRUCTION_SCHEDULED";
        public const string PixAutomaticPaymentInstructionRefused = "PIX_AUTOMATIC_RECURRING_PAYMENT_INSTRUCTION_REFUSED";
        public const string PixAutomaticPaymentInstructionCancelled = "PIX_AUTOMATIC_RECURRING_PAYMENT_INSTRUCTION_CANCELLED";
        public const string SubscriptionCreated = "SUBSCRIPTION_CREATED";
        public const string SubscriptionUpdated = "SUBSCRIPTION_UPDATED";
        public const string SubscriptionInactivated = "SUBSCRIPTION_INACTIVATED";
        public const string SubscriptionDeleted = "SUBSCRIPTION_DELETED";
    }
    public static class AsaasPaymentStatus
    {
        public const string Pending = "PENDING";
        public const string Overdue = "OVERDUE";
        public const string Received = "RECEIVED";
        public const string Confirmed = "CONFIRMED";
        public const string Deleted = "DELETED";
        public const string Refunded = "REFUNDED";
        public const string Refused = "REFUSED";
        public const string Dunning = "DUNNING";
        public const string ChargebackRequested = "CHARGEBACK_REQUESTED";
        public const string ChargebackDispute = "CHARGEBACK_DISPUTE";
        public const string AwaitingRiskAnalysis = "AWAITING_RISK_ANALYSIS";
    }

    /// <summary>Status de assinatura Asaas (campo status em POST/GET /subscriptions).</summary>
    public static class AsaasSubscriptionStatus
    {
        public const string Active = "ACTIVE";
        public const string Inactive = "INACTIVE";
        public const string Expired = "EXPIRED";
    }
    public static class AsaasWebhookPayloadKeys
    {
        public const string Event = "event";
        public const string Payment = "payment";
        public const string Authorization = "authorization";
        public const string PaymentInstruction = "paymentInstruction";
        public const string Id = "id";
        public const string Status = "status";
        public const string Subscription = "subscription";
        public const string Value = "value";
        public const string BillingType = "billingType";
        public const string DueDate = "dueDate";
        public const string ExternalReference = "externalReference";
        public const string ContractId = "contractId";
        public const string Customer = "customer";
        public const string CustomerId = "customerId";
        public const string ImmediateQrCode = "immediateQrCode";
        public const string ConciliationIdentifier = "conciliationIdentifier";
        public const string PaymentId = "paymentId";
        public const string NextDueDate = "nextDueDate";
        public const string Cycle = "cycle";
    }

    /// <summary>Motivos de falha gravados em Payment.FailureReason para PIX Automático (autorização).</summary>
    public static class AsaasPixAuthorizationFailureReasons
    {
        public const string Refused = "Autorização PIX Automático recusada pelo banco. Gere um novo QR em Detalhes do pagamento.";
        public const string Expired = "Autorização PIX Automático expirada sem pagamento. Gere um novo QR em Detalhes do pagamento.";
        public const string Cancelled = "Autorização PIX Automático cancelada.";
    }

    public static class AsaasBillingType
    {
        public const string CreditCard = "CREDIT_CARD";
        public const string Pix = "PIX";
        public const string Boleto = "BOLETO";
    }

    /// <summary>Ciclos de assinatura Asaas (campo cycle em POST /subscriptions).</summary>
    public static class AsaasSubscriptionCycle
    {
        public const string Monthly = "MONTHLY";
        public const string Yearly = "YEARLY";
    }
}