namespace KlyonHubPlatform.Application.DTOs.Dashboard
{
    public class UserDashboardItemDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public bool EmailVerified { get; set; }
    }
}
