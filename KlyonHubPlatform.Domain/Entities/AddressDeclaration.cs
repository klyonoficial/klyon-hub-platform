namespace KlyonHubPlatform.Domain.Entities
{
    public class AddressDeclaration
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public string DeclarationNumber { get; set; } = string.Empty; // Número único da declaração
        public int Version { get; set; } = 1; // Versão da declaração (incrementa a cada nova geração)
        public DateTime StartDate { get; set; } // Data inicial da autorização
        public DateTime EndDate { get; set; } // Data final da autorização (ExpiryDate do contrato)
        public string FilePath { get; set; } = string.Empty; // Caminho do PDF gerado
        public string? DigitalSignature { get; set; } // Hash da assinatura digital
        public bool IsValid { get; set; } = true; // Se a declaração está válida
        public DateTime? InvalidatedAt { get; set; } // Data de invalidação
        public string? InvalidationReason { get; set; } // Motivo da invalidação
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Contract Contract { get; set; } = null!;
    }
}
