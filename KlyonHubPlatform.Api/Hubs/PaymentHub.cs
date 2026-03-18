using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace KlyonHubPlatform.Api.Hubs;

/// <summary>
/// Hub para notificações em tempo real de pagamento (ex.: confirmação via webhook Asaas).
/// O cliente na tela PaymentDetails entra no grupo do pagamento e recebe PaymentConfirmed ou PaymentCancelled.
/// </summary>
[Authorize]
public class PaymentHub : Hub
{
    public const string PaymentConfirmed = "PaymentConfirmed";
    public const string PaymentCancelled = "PaymentCancelled";
    /// <summary>Novo código PIX gerado após vencimento (PAYMENT_OVERDUE).</summary>
    public const string PaymentPixCodeRegenerated = "PaymentPixCodeRegenerated";

    /// <summary>
    /// Adiciona a conexão ao grupo do pagamento para receber notificações em tempo real.
    /// </summary>
    public async Task JoinPaymentGroup(int paymentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"payment_{paymentId}");
    }
}
