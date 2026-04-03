namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>Estado do lead no fluxo CNPJ primeiro (captura → PIX adesão → acesso mágico).</summary>
    public enum FiscalOnboardingLeadStatus
    {
        Iniciado = 0,
        AguardandoPagamento = 1,
        PagamentoConfirmado = 2,
        AcessoGerado = 3
    }
}
