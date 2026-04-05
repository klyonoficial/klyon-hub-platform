namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true);
        Task<bool> SendVerificationEmailAsync(string to, string name, string token, string? returnUrl = null);
        Task<bool> SendPasswordResetEmailAsync(string to, string name, string token);
        /// <summary>Boas-vindas ao cliente que efetuou onboarding (Fiscal ou Comercial) — contém link para definir a senha.</summary>
        Task<bool> SendClientWelcomeEmailAsync(string to, string name, string token, string serviceDescription = "Klyon");
        Task<bool> SendContractExpiryNotificationAsync(string to, string clientName, string contractNumber, DateTime expiryDate, int daysRemaining);
        Task<bool> SendReservationRequestToCoworkingAsync(string to, string coworkingName, string clientName, IEnumerable<(DateTime Date, TimeSpan? Start, TimeSpan? End, string RoomType)> items);
        Task<bool> SendReservationRequestToClientAsync(string to, string clientName, IEnumerable<(DateTime Date, TimeSpan? Start, TimeSpan? End, string RoomType)> items);
        Task<bool> SendClientCredentialsEmailAsync(string to, string name, string email, string password, string paymentDetailsUrl);
        Task<bool> SendCorrespondenceArrivalNotificationAsync(string to, string clientName, string addressInfo, DateTime arrivalDate, string schedulePickupUrl);
    }
}