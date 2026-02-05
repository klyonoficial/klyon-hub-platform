namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Ambiente da API D4Sign. Sandbox não possui validade jurídica; use para testes.
    /// </summary>
    public enum D4SignEnvironment
    {
        /// <summary>Ambiente de desenvolvimento (sem validade jurídica).</summary>
        Sandbox = 0,

        /// <summary>Ambiente de produção (validade jurídica).</summary>
        Production = 1
    }
}
