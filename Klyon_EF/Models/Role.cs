namespace KlyonEF.Models
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public static class RoleNames
    {
        public const string Administrator = "Administrator";
        public const string CoworkingPartner = "CoworkingPartner";
        public const string Client = "Client";
    }
}
