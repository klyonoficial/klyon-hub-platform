namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>Destino de navegação do cliente a partir de listagens de pagamento (MyPayments).</summary>
    public enum PaymentClientDestination
    {
        None = 0,
        PaymentDetails,
        PaymentSuccess,
        PaymentSummary
    }
}
