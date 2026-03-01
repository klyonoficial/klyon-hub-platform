namespace KlyonHubPlatform.Api.Constants
{
    public static class AuthConstants
    {
        public const string CookieScheme = "Cookies";
        public const string DefaultUserIdClaimValue = "0";
        public const string TokenClaimType = "Token";
        /// <summary>
        /// Claim "true" quando o cliente (Empresa/Profissional) informou no registro que possui CNPJ e deve ver a seção de upload de documentos.
        /// </summary>
        public const string ClientHasCnpjClaimType = "ClientHasCnpj";
    }
}