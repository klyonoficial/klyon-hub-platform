namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Método de autenticação no embed (createlist - embed_methodauth).
    /// Valores aceitos pela API: email, sms, etc.
    /// </summary>
    public enum D4SignEmbedMethodAuth
    {
        /// <summary>Autenticação por e-mail.</summary>
        Email,

        /// <summary>Autenticação por SMS.</summary>
        Sms
    }

    /// <summary>
    /// Extensões para obter o valor string da API D4Sign.
    /// </summary>
    public static class D4SignEmbedMethodAuthExtensions
    {
        public static string ToApiValue(this D4SignEmbedMethodAuth value)
        {
            return value switch
            {
                D4SignEmbedMethodAuth.Email => "email",
                D4SignEmbedMethodAuth.Sms => "sms",
                _ => "email"
            };
        }
    }
}
