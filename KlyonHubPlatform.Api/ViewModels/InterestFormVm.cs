namespace KlyonHubPlatform.Api.ViewModels
{
    public class InterestFormVm
    {
        public int CoworkingAddressId { get; set; }
        public DateTime? AllocationDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }
}