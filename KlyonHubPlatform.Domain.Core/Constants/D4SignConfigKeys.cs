namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Chaves de configuração do sistema para integração D4Sign (assinatura eletrônica).
    /// Documentação: https://docapi.d4sign.com.br/docs/introdução-a-api
    /// </summary>
    public static class D4SignConfigKeys
    {
        /// <summary>Token da API (menu Dev API na conta D4Sign). Obrigatório.</summary>
        public const string TokenAPI = "D4Sign.TokenAPI";

        /// <summary>Chave criptográfica (obrigatória se habilitada na conta D4Sign).</summary>
        public const string CryptKey = "D4Sign.CryptKey";

        /// <summary>Usar ambiente sandbox (true) ou produção (false). Para testes use true.</summary>
        public const string UseSandbox = "D4Sign.UseSandbox";

        /// <summary>UUID do cofre onde os documentos serão armazenados (GET /safes). Obrigatório.</summary>
        public const string SafeUuid = "D4Sign.SafeUuid";
    }
}
