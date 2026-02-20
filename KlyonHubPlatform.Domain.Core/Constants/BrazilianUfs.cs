namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Siglas das Unidades Federativas do Brasil (UF).
    /// Use esta classe em vez de repetir listas de estados em vários pontos do código.
    /// </summary>
    public static class BrazilianUfs
    {
        /// <summary>Siglas das 27 UFs na ordem: AC, AL, AP, AM, BA, CE, DF, ES, GO, MA, MT, MS, MG, PA, PB, PR, PE, PI, RJ, RN, RS, RO, RR, SC, SP, SE, TO.</summary>
        public static readonly string[] All =
        {
            "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
            "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
            "RS", "RO", "RR", "SC", "SP", "SE", "TO"
        };

        /// <summary>Nomes dos estados na mesma ordem que <see cref="All"/> (para dropdowns e exibição).</summary>
        public static readonly string[] Names =
        {
            "Acre", "Alagoas", "Amapá", "Amazonas", "Bahia", "Ceará", "Distrito Federal",
            "Espírito Santo", "Goiás", "Maranhão", "Mato Grosso", "Mato Grosso do Sul",
            "Minas Gerais", "Pará", "Paraíba", "Paraná", "Pernambuco", "Piauí",
            "Rio de Janeiro", "Rio Grande do Norte", "Rio Grande do Sul", "Rondônia",
            "Roraima", "Santa Catarina", "São Paulo", "Sergipe", "Tocantins"
        };
    }
}
