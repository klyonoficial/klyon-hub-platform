namespace KlyonHubPlatform.Application.DTOs.Admin
{
    /// <summary>Preview do usuário alvo antes da exclusão em cascata (purge).</summary>
    public class UserPurgeLookupDto
    {
        public bool Found { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        /// <summary>Client | CoworkingPartner | Administrator | Unknown</summary>
        public string RoleType { get; set; } = "Unknown";
        public string RoleDisplayName { get; set; } = string.Empty;
        public bool CanDelete { get; set; }
        public string? BlockReason { get; set; }
    }

    /// <summary>Resultado da exclusão em cascata de dados do usuário.</summary>
    public class UserPurgeResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? RoleType { get; set; }
    }
}
