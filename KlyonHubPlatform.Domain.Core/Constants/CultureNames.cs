namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Nomes de cultura (locale) suportados pela aplicação.
    /// Use estas constantes em vez de literais "pt-BR", "en-US", etc.
    /// </summary>
    public static class CultureNames
    {
        /// <summary>Cultura padrão (Brasil - português).</summary>
        public const string PtBr = "pt-BR";

        /// <summary>Inglês (EUA).</summary>
        public const string EnUs = "en-US";

        /// <summary>Espanhol (Espanha).</summary>
        public const string EsEs = "es-ES";

        /// <summary>Cultura padrão da aplicação.</summary>
        public const string Default = PtBr;
    }
}
