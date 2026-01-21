namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Saldo de créditos da plataforma (conta do administrador).
    /// Créditos entram na compra do cliente; saem quando repassados ao coworking (após dupla confirmação).
    /// A comissão fica retida (não é repassada), então não há débito correspondente.
    /// </summary>
    public class PlatformCreditBalance
    {
        public int Id { get; set; }
        public decimal TotalCredits { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
