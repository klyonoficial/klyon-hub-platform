namespace KlyonHubPlatform.Api.Constants
{
    /// <summary>
    /// Constantes de autenticação (scheme, fallbacks, etc.).
    /// </summary>
    public static class AuthConstants
    {
        /// <summary>Nome do scheme de autenticação por cookie.</summary>
        public const string CookieScheme = "Cookies";

        /// <summary>Valor usado como fallback quando ClaimTypes.NameIdentifier não está presente (ex.: int.Parse).</summary>
        public const string DefaultUserIdClaimValue = "0";
    }
}
