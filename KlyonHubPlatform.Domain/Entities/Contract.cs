using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    public class Contract
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public int CoworkingAddressId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? ExpiryDate { get; set; } // Data de validade do endereço fiscal (1 ano após primeiro pagamento)
        public DateTime? LastExpiryNotificationSent { get; set; } // Última vez que foi enviada notificação de vencimento
        public ContractStatus Status { get; set; } = ContractStatus.Active;
        public decimal MonthlyValue { get; set; }
        public string? ContractDocumentPath { get; set; }
        public string? SignedBy { get; set; }
        public DateTime? SignedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime? CancellationRequestedAt { get; set; }
        public string? CancellationRequestReason { get; set; }
        public int? CancellationApprovedBy { get; set; }
        public DateTime? CancellationApprovedAt { get; set; }
        public string? CancellationRejectionReason { get; set; }

        // Campos congelados (snapshot) - valores imutáveis durante a vigência do contrato
        /// <summary>Valor mensal do contrato no momento da criação (congelado)</summary>
        public decimal SnapshotMonthlyValue { get; set; }
        /// <summary>Percentual de comissão da plataforma no momento da criação (congelado)</summary>
        public decimal SnapshotPlatformCommissionPercentage { get; set; }
        /// <summary>Percentual de comissão do coworking no momento da criação (congelado)</summary>
        public decimal SnapshotCoworkingCommissionPercentage { get; set; }
        /// <summary>Endereço completo contratado no momento da criação (congelado)</summary>
        public string SnapshotAddress { get; set; } = string.Empty;
        /// <summary>Percentual aplicado no momento da criação (congelado)</summary>
        public decimal SnapshotCommissionPercentage { get; set; }
        /// <summary>Capacidade consumida no momento da criação (congelado)</summary>
        public int SnapshotCapacityConsumed { get; set; }

        /// <summary>Indica se o contrato foi criado via painel administrativo (venda assistida)</summary>
        public bool IsAssistedSale { get; set; }
        /// <summary>ID do admin responsável pela venda assistida (quando criado pelo painel)</summary>
        public int? AssistedSaleAdminId { get; set; }

        // Navigation properties
        public virtual Client Client { get; set; } = null!;
        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
        public virtual Subscription? Subscription { get; set; }
        public virtual User? AssistedSaleAdmin { get; set; }
        public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}

