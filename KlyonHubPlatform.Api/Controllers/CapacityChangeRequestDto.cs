namespace KlyonHubPlatform.Api.Controllers
{
    /// <summary>DTO para solicitação de alteração de capacidade.</summary>
    public class CapacityChangeRequestDto
    {
        public int AddressId { get; set; }
        public int RequestedCapacity { get; set; }
        public string? Justification { get; set; }
    }
}
