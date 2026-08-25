using System.Text.RegularExpressions;

namespace KlyonHubPlatform.Application.Helpers
{
    public static class BrazilianTaxId
    {
        public static string Digits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return Regex.Replace(value, @"[^\d]", "");
        }

        public static bool IsBlank(string? value)
        {
            var digits = Digits(value);
            return digits.Length == 0 || digits.All(c => c == '0');
        }

        public static bool IsValidCpf(string? value)
        {
            var cpf = Digits(value);
            if (cpf.Length != 11 || cpf.All(c => c == cpf[0]))
                return false;

            int[] m1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] m2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };
            var temp = cpf[..9];
            var sum = 0;
            for (var i = 0; i < 9; i++)
                sum += (temp[i] - '0') * m1[i];
            var d1 = sum % 11;
            d1 = d1 < 2 ? 0 : 11 - d1;
            temp += d1.ToString();
            sum = 0;
            for (var i = 0; i < 10; i++)
                sum += (temp[i] - '0') * m2[i];
            var d2 = sum % 11;
            d2 = d2 < 2 ? 0 : 11 - d2;
            return cpf.EndsWith($"{d1}{d2}", StringComparison.Ordinal);
        }

        public static bool IsValidCnpj(string? value)
        {
            var cnpj = Digits(value);
            if (cnpj.Length != 14 || cnpj.All(c => c == cnpj[0]))
                return false;

            int[] m1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] m2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            var temp = cnpj[..12];
            var sum = 0;
            for (var i = 0; i < 12; i++)
                sum += (temp[i] - '0') * m1[i];
            var d1 = sum % 11;
            d1 = d1 < 2 ? 0 : 11 - d1;
            temp += d1.ToString();
            sum = 0;
            for (var i = 0; i < 13; i++)
                sum += (temp[i] - '0') * m2[i];
            var d2 = sum % 11;
            d2 = d2 < 2 ? 0 : 11 - d2;
            return cnpj.EndsWith($"{d1}{d2}", StringComparison.Ordinal);
        }

        public static string? FormatCpf(string? value)
        {
            var digits = Digits(value);
            if (digits.Length != 11)
                return null;
            return $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}";
        }

        public static string? FormatCnpj(string? value)
        {
            var digits = Digits(value);
            if (digits.Length != 14)
                return null;
            return $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..]}";
        }
    }
}
