namespace KlyonHubPlatform.Domain.Core.Constants
{
    public static class FiscalNoteType
    {
        public const string Intermediacao = "Intermediacao";
        public const string Servico = "Servico";
        public const string Default = Intermediacao;
        public static string ToDisplayString(string? type)
        {
            if (string.IsNullOrEmpty(type)) return EnumDisplayConstants.Unknown;
            if (type == Intermediacao) return DisplayStrings.Intermediacao;
            if (type == Servico) return DisplayStrings.Servico;
            return type;
        }
        public static class DisplayStrings
        {
            public const string Intermediacao = "Intermediação";
            public const string Servico = "Serviço";
        }
    }
}