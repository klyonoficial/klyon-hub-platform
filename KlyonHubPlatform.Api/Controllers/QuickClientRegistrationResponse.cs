namespace KlyonHubPlatform.Api.Controllers
{
    /// <summary>
    /// Response do cadastro rápido de cliente
    /// </summary>
    public class QuickClientRegistrationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? ContractId { get; set; }
        public string? ContractNumber { get; set; }
        public int? ClientId { get; set; }
        public int? PaymentId { get; set; }
        public string? PixCode { get; set; }
        public int? UserId { get; set; }
        public bool IsNewUser { get; set; }
    }
}
