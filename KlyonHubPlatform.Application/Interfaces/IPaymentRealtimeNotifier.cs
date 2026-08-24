namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IPaymentRealtimeNotifier
    {
        Task NotifyPaymentConfirmedAsync(int paymentId, CancellationToken cancellationToken = default);
        Task NotifyPaymentCancelledAsync(int paymentId, CancellationToken cancellationToken = default);
        Task NotifyPaymentPixCodeRegeneratedAsync(int paymentId, CancellationToken cancellationToken = default);
    }
}
