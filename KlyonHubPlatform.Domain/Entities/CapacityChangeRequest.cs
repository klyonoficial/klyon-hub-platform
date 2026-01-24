using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>
    /// Solicitação de alteração de capacidade fiscal de um endereço.
    /// </summary>
    public class CapacityChangeRequest
    {
        public int Id { get; set; }
        public int CoworkingAddressId { get; set; }
        /// <summary>Capacidade solicitada pelo parceiro</summary>
        public int RequestedCapacity { get; set; }
        /// <summary>Capacidade anterior (antes da solicitação)</summary>
        public int PreviousCapacity { get; set; }
        /// <summary>Status da solicitação</summary>
        public CapacityApprovalStatus Status { get; set; } = CapacityApprovalStatus.Pending;
        /// <summary>Justificativa do parceiro para a solicitação</summary>
        public string? Justification { get; set; }
        /// <summary>Motivo da reprovação (se aplicável)</summary>
        public string? RejectionReason { get; set; }
        /// <summary>ID do administrador que aprovou/reprovou</summary>
        public int? ReviewedBy { get; set; }
        /// <summary>Data da revisão</summary>
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
        public virtual User? Reviewer { get; set; }
    }
}
