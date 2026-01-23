namespace KlyonHubPlatform.Domain.Entities
{
    public class CreditPlan
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // "Básico", "Profissional", "Business"
        public decimal Price { get; set; }
        public int Credits { get; set; }
        public int BonusCredits { get; set; } = 0;
        public string Period { get; set; } = "Mensal"; // "Mensal", "Semestral", "Anual"
        public int ValidityMonths { get; set; } = 6; // Validade dos créditos em meses
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

