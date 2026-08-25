namespace KlyonHubPlatform.Application.DTOs.Contracts
{
    public class ContractTimelineItemDto
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }
}
