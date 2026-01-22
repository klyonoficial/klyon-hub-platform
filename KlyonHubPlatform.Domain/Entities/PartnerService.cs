namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Tabela intermediária: Relaciona Parceiro (Coworking) com Serviço e define percentagem fechada em contrato
    /// </summary>
    public class PartnerService
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public int ServiceId { get; set; }
        /// <summary>Percentagem fechada em contrato (definida pelo administrador, não pode ser alterada pelo parceiro)</summary>
        public decimal Percentage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
        public virtual Service Service { get; set; } = null!;
    }
}
