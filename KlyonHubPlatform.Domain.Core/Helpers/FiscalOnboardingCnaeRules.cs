using System.Globalization;

namespace KlyonHubPlatform.Domain.Core.Helpers
{
    /// <summary>
    /// CNAE (subclasse 7 dígitos): bloqueios para contratação de endereço fiscal conforme política de negócio.
    /// Indústria/fabricação (divisões 10–33), varejo (47), transporte e armazenagem (49–52).
    /// </summary>
    public static class FiscalOnboardingCnaeRules
    {
        /// <summary>Retorna true se o CNAE (principal ou secundário) não pode usar endereço fiscal.</summary>
        public static bool IsCnaeBlocked(string? cnaeRaw, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(cnaeRaw))
                return false;

            var digits = new string(cnaeRaw.Where(char.IsDigit).ToArray());
            if (digits.Length < 7)
                return false;

            digits = digits.Length > 7 ? digits[^7..] : digits.PadLeft(7, '0');
            if (!int.TryParse(digits.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var d2))
                return false;

            if (d2 >= 10 && d2 <= 33)
            {
                reason = "O CNAE informado indica atividade de indústria ou fabricação, não permitida para este serviço de endereço fiscal.";
                return true;
            }

            if (d2 == 47)
            {
                reason = "O CNAE informado indica comércio varejista (circulação física de mercadorias), não permitido para este serviço.";
                return true;
            }

            if (d2 >= 49 && d2 <= 52)
            {
                reason = "O CNAE informado indica transporte, armazenagem ou atividades de logística, não permitidas para este serviço.";
                return true;
            }

            return false;
        }

        /// <summary>Verifica principal e secundários: bloqueia se qualquer um estiver na lista restrita.</summary>
        public static bool AnyCnaeBlocked(string? cnaePrincipal, IEnumerable<string>? secundarios, out string reason)
        {
            reason = string.Empty;
            if (IsCnaeBlocked(cnaePrincipal, out reason))
                return true;
            if (secundarios == null)
                return false;
            foreach (var s in secundarios)
            {
                if (IsCnaeBlocked(s, out reason))
                    return true;
            }
            return false;
        }
    }
}
