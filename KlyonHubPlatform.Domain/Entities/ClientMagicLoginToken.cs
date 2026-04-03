namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>Token de uso único para login automático após adesão PIX.</summary>
    public class ClientMagicLoginToken
    {
        public int Id { get; set; }
        public int FiscalOnboardingLeadId { get; set; }
        /// <summary>Segredo opaco (URL-safe).</summary>
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public virtual FiscalOnboardingLead FiscalOnboardingLead { get; set; } = null!;
    }
}
