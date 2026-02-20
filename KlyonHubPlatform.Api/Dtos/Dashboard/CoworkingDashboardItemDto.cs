namespace KlyonHubPlatform.Api.Dtos.Dashboard
{
    public class CoworkingDashboardItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Cnpj { get; set; }
        public bool IsActive { get; set; }
        public string ApprovalStatus { get; set; } = string.Empty;
        public string ApprovalStatusDisplay { get; set; } = string.Empty;
    }
}
