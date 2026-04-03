namespace KlyonHubPlatform.Application.DTOs.FiscalOnboarding
{
    /// <summary>Bloqueios de UX no início do onboarding fiscal (CNPJ + e-mail já existentes).</summary>
    public enum FiscalOnboardingStartBlockedKind
    {
        None = 0,
        /// <summary>CNPJ cadastrado com outro e-mail — orientar login para atualizar contato.</summary>
        EmailMismatchForTaxId = 1,
        /// <summary>Já há adesão/contrato ativo neste endereço fiscal.</summary>
        AlreadyHasFiscalAddressActive = 2
    }
}
