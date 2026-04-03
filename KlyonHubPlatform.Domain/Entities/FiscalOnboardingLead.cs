using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Domain.Entities
{
    /// <summary>Lead capturado antes de conta de usuário (fluxo CNPJ + PIX adesão).</summary>
    public class FiscalOnboardingLead
    {
        public int Id { get; set; }
        /// <summary>Parceiro (coworking) do endereço fiscal escolhido.</summary>
        public int CoworkingId { get; set; }
        public int CoworkingAddressId { get; set; }
        /// <summary>Indica se o cliente declarou possuir CNPJ (documentação fiscal CNPJ).</summary>
        public bool DeclaresHasCnpj { get; set; }
        /// <summary>CPF (11) ou CNPJ (14) apenas dígitos.</summary>
        public string TaxIdNormalized { get; set; } = string.Empty;
        /// <summary>E-mail para envio do link de acesso após pagamento.</summary>
        public string ContactEmail { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }
        /// <summary>Dados da consulta OpenCNPJ (somente fluxo CNPJ).</summary>
        public string? CnpjRazaoSocialSnapshot { get; set; }
        public string? CnpjCnaePrincipalSnapshot { get; set; }
        public string? AsaasCustomerId { get; set; }
        public FiscalOnboardingLeadStatus Status { get; set; } = FiscalOnboardingLeadStatus.Iniciado;
        /// <summary>Token opaco na URL da tela de PIX (sem expor id sequencial).</summary>
        public Guid PublicPaymentToken { get; set; }
        public int? UserId { get; set; }
        public int? ClientId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual Coworking Coworking { get; set; } = null!;
        public virtual CoworkingAddress CoworkingAddress { get; set; } = null!;
        public virtual User? User { get; set; }
        public virtual Client? Client { get; set; }
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public virtual ICollection<ClientMagicLoginToken> MagicLoginTokens { get; set; } = new List<ClientMagicLoginToken>();
    }
}
