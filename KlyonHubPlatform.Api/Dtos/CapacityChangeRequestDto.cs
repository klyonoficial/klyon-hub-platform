namespace KlyonHubPlatform.Api.Dtos
{
    public class CapacityChangeRequestDto
    {
        public int AddressId { get; set; }
        public int RequestedCapacity { get; set; }
        public string? Justification { get; set; }
    }
}