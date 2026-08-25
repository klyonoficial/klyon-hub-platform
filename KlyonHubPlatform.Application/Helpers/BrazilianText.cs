using System.Globalization;
using System.Text.RegularExpressions;

namespace KlyonHubPlatform.Application.Helpers
{
    /// <summary>
    /// Normalização de textos brasileiros para cadastro (perfil, gateway).
    /// Title Case pt-BR sensato; não altera CPF/CNPJ/CEP/telefone (use <see cref="BrazilianTaxId"/>).
    /// </summary>
    public static class BrazilianText
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
        private static readonly Regex MultiSpace = new(@"\s+", RegexOptions.Compiled);

        /// <summary>Preposições/conjunções em minúsculo no meio do nome (primeira palavra sempre capitalizada).</summary>
        private static readonly HashSet<string> LowercaseParticles = new(StringComparer.OrdinalIgnoreCase)
        {
            "da", "de", "do", "dos", "das", "e"
        };

        /// <summary>
        /// Nome completo, razão social, nome fantasia, logradouro, complemento, bairro ou cidade em Title Case pt-BR.
        /// Ex.: <c>JOÃO DA SILVA</c> → <c>João da Silva</c>. Retorna null se vazio.
        /// </summary>
        public static string? NormalizePersonName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = MultiSpace.Replace(value.Trim(), " ");
            var parts = trimmed.Split(' ');
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part.Length == 0)
                    continue;

                if (i > 0 && LowercaseParticles.Contains(part))
                {
                    parts[i] = part.ToLower(PtBr);
                    continue;
                }

                parts[i] = CapitalizeToken(part);
            }

            return string.Join(' ', parts);
        }

        /// <summary>E-mail em minúsculas. Retorna null se vazio.</summary>
        public static string? NormalizeEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;
            return email.Trim().ToLowerInvariant();
        }

        /// <summary>UF em maiúsculo, no máximo 2 letras. Retorna null se vazio.</summary>
        public static string? NormalizeUf(string? state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return null;
            var uf = state.Trim().ToUpperInvariant();
            return uf.Length <= 2 ? uf : uf[..2];
        }

        private static string CapitalizeToken(string token)
        {
            if (token.Contains('-', StringComparison.Ordinal))
            {
                var segments = token.Split('-');
                for (var i = 0; i < segments.Length; i++)
                    segments[i] = CapitalizeWord(segments[i]);
                return string.Join('-', segments);
            }

            return CapitalizeWord(token);
        }

        private static string CapitalizeWord(string word)
        {
            if (string.IsNullOrEmpty(word))
                return word;

            var lower = word.ToLower(PtBr);
            if (lower.Length == 1)
                return lower.ToUpper(PtBr);

            return char.ToUpper(lower[0], PtBr) + lower[1..];
        }
    }
}
