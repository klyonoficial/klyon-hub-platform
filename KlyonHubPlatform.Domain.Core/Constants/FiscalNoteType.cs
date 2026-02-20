namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Tipos de nota fiscal (valor armazenado no banco e em APIs).
    /// Use estas constantes em vez de literais "Servico" e "Intermediacao".
    /// </summary>
    public static class FiscalNoteType
    {
        /// <summary>Intermediação (plataforma/comissão).</summary>
        public const string Intermediacao = "Intermediacao";

        /// <summary>Serviço (parceiro/coworking).</summary>
        public const string Servico = "Servico";

        /// <summary>Valor padrão para novas entidades (intermediação).</summary>
        public const string Default = Intermediacao;

        /// <summary>Texto de exibição para o tipo (dropdowns, labels).</summary>
        public static string ToDisplayString(string? type)
        {
            if (string.IsNullOrEmpty(type)) return EnumDisplayConstants.Unknown;
            if (type == Intermediacao) return DisplayStrings.Intermediacao;
            if (type == Servico) return DisplayStrings.Servico;
            return type;
        }

        /// <summary>Strings de exibição para os tipos de NF.</summary>
        public static class DisplayStrings
        {
            public const string Intermediacao = "Intermediação";
            public const string Servico = "Serviço";
        }
    }
}
