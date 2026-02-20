namespace KlyonHubPlatform.Application.DTOs.AppHome
{
    public class CreateReservationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int? PaymentId { get; set; }
    }
}
