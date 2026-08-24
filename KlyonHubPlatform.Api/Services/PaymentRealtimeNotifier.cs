using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace KlyonHubPlatform.Api.Services
{
    public class PaymentRealtimeNotifier : IPaymentRealtimeNotifier
    {
        private readonly IHubContext<PaymentHub> _hubContext;
        private readonly ILogger<PaymentRealtimeNotifier> _logger;

        public PaymentRealtimeNotifier(IHubContext<PaymentHub> hubContext, ILogger<PaymentRealtimeNotifier> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyPaymentConfirmedAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            await SendAsync(PaymentHub.PaymentConfirmed, paymentId, cancellationToken);
        }

        public async Task NotifyPaymentCancelledAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            await SendAsync(PaymentHub.PaymentCancelled, paymentId, cancellationToken);
        }

        public async Task NotifyPaymentPixCodeRegeneratedAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            await SendAsync(PaymentHub.PaymentPixCodeRegenerated, paymentId, cancellationToken);
        }

        private async Task SendAsync(string eventName, int paymentId, CancellationToken cancellationToken)
        {
            try
            {
                await _hubContext.Clients
                    .Group($"payment_{paymentId}")
                    .SendAsync(eventName, new { paymentId }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao notificar SignalR ({Event}) para pagamento {PaymentId}", eventName, paymentId);
            }
        }
    }
}
