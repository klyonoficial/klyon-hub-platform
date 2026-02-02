namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true);
        Task<bool> SendVerificationEmailAsync(string to, string name, string token, string? returnUrl = null);
        Task<bool> SendPasswordResetEmailAsync(string to, string name, string token);
        Task<bool> SendContractExpiryNotificationAsync(string to, string clientName, string contractNumber, DateTime expiryDate, int daysRemaining);
        /// <summary>Envia ao Coworking: solicitação de reserva(s), link para aprovar/reprovar, aviso 24h.</summary>
        Task<bool> SendReservationRequestToCoworkingAsync(string to, string coworkingName, string clientName, IEnumerable<(DateTime Date, TimeSpan? Start, TimeSpan? End, string RoomType)> items);
        /// <summary>Envia ao Cliente: confirmação de que a solicitação foi enviada e retorno em até 24h.</summary>
        Task<bool> SendReservationRequestToClientAsync(string to, string clientName, IEnumerable<(DateTime Date, TimeSpan? Start, TimeSpan? End, string RoomType)> items);
        /// <summary>Envia credenciais de acesso ao cliente recém-cadastrado com link para PaymentDetails.</summary>
        Task<bool> SendClientCredentialsEmailAsync(string to, string name, string email, string password, string paymentDetailsUrl);
    }
}
