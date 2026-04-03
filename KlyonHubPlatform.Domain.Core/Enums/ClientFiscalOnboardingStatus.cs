namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>Evolução do cadastro fiscal após adesão paga (passos 12–14 do onboarding).</summary>
    public enum ClientFiscalOnboardingStatus
    {
        PagamentoAdesaoConfirmado = 1,
        PerfilCompleto = 2,
        DocumentosEnviados = 3,
        ContratoFinalizado = 4
    }
}
