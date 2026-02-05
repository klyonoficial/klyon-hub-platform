namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Chaves e valores de configuração para provedor de assinatura eletrônica de contratos.
    /// Permite escolher entre D4Sign e Clicksign quando ambos estão configurados.
    /// </summary>
    public static class ContractSigningConfigKeys
    {
        /// <summary>Chave que define qual provedor usar: "D4Sign" ou "Clicksign".</summary>
        public const string Provider = "ContractSigning.Provider";

        /// <summary>Valor para usar D4Sign como provedor de assinatura.</summary>
        public const string ProviderD4Sign = "D4Sign";

        /// <summary>Valor para usar Clicksign como provedor de assinatura.</summary>
        public const string ProviderClicksign = "Clicksign";
    }
}
