namespace KlyonHubPlatform.Domain.Entities
{
    public class CoworkingPartner
    {
        public int Id { get; set; }
        public int CoworkingId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CPF { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Role { get; set; } // Cargo/função: lista fixa (Sócio, Diretor, Gerente, etc.)
        public decimal? SharePercentage { get; set; } // Percentual de participação
        public string? PhotoPath { get; set; } // Foto do sócio (upload)
        public bool IsPrincipal { get; set; } // Sócio principal – exibido ao clicar no coworking
        public bool IsContractSigner { get; set; } // Sócio assinante do contrato
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Coworking Coworking { get; set; } = null!;
    }
}

