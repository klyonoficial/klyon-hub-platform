namespace KlyonHubPlatform.Domain.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public bool IsActive { get; set; } = true;
        public bool EmailVerified { get; set; } = false;
        public bool MustChangePassword { get; set; } = false;
        public string? EmailVerificationToken { get; set; }
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetExpires { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        /// <summary>
        /// Para usuários com role Client: true = possui CNPJ e ver seção de documentos no cadastro; false = não possui; null = não definido ou usuário antigo.
        /// </summary>
        public bool? ClientHasCnpj { get; set; }
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}