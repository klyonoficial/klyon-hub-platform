using System.Linq;

namespace KlyonHubPlatform.Application.Helpers
{
    /// <summary>
    /// Padronização e validação FEBRABAN: código de barras (44 dígitos) e linha digitável (47 caracteres formatados).
    /// </summary>
    public static class BoletoHelper
    {
        private const int BarcodeLength = 44;
        private const int DigitableLineDigitsLength = 47;

        /// <summary>
        /// Extrai apenas os dígitos da string (remove pontos, espaços, etc.).
        /// </summary>
        public static string OnlyDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return new string(value.Where(char.IsDigit).ToArray());
        }

        /// <summary>
        /// Calcula o dígito verificador módulo 10 (FEBRABAN) para um bloco de 10 dígitos.
        /// Multiplicadores 2,1,2,1... da direita para a esquerda; soma dos dígitos dos produtos; DAC = 10 - (soma % 10); se 10 então 0.
        /// </summary>
        private static int Mod10Dac(string block)
        {
            if (string.IsNullOrEmpty(block) || block.Length != 10) return -1;
            int sum = 0;
            for (int i = 0; i < 10; i++)
            {
                if (!char.IsDigit(block[i])) return -1;
                int digit = block[i] - '0';
                int multiplier = (9 - i) % 2 == 0 ? 2 : 1;
                int product = digit * multiplier;
                sum += product >= 10 ? (product / 10) + (product % 10) : product;
            }
            int dac = 10 - (sum % 10);
            return dac == 10 ? 0 : dac;
        }

        /// <summary>
        /// Calcula o dígito verificador módulo 11 (FEBRABAN) do código de barras de 44 dígitos.
        /// O DV está na posição 4 (0-based). Multiplicadores 2-9 da direita para a esquerda; DAC = 11 - (soma % 11); se 0/10/11 então 1.
        /// </summary>
        private static int Mod11DacBarcode44(string barcode)
        {
            if (string.IsNullOrEmpty(barcode) || barcode.Length != BarcodeLength) return -1;
            int sum = 0;
            int mult = 2;
            for (int i = 43; i >= 0; i--)
            {
                if (i == 4) continue;
                if (!char.IsDigit(barcode[i])) return -1;
                int digit = barcode[i] - '0';
                sum += digit * mult;
                mult = mult >= 9 ? 2 : mult + 1;
            }
            int rest = sum % 11;
            int dac = 11 - rest;
            return (dac is 0 or 1 or 10 or 11) ? 1 : dac;
        }

        /// <summary>
        /// Valida os valores recebidos da integração (Asaas) conforme padrão FEBRABAN.
        /// Retorna true se a linha digitável (47 dígitos) e o código de barras derivado (44) passam nos dígitos verificadores (módulo 10 e 11).
        /// </summary>
        public static (bool IsValid, string? ErrorMessage) ValidateBoletoFromIntegration(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return (false, "Valor do boleto não informado.");
            var digits = OnlyDigits(rawValue);
            if (digits.Length != DigitableLineDigitsLength)
                return (false, $"Linha digitável deve ter 47 dígitos (recebido: {digits.Length} após normalização).");
            int dac1 = Mod10Dac(digits[..10]);
            if (dac1 < 0) return (false, "Bloco 1 da linha digitável inválido.");
            if (dac1 != (digits[10] - '0')) return (false, "Dígito verificador do bloco 1 (linha digitável) não confere (módulo 10).");
            int dac2 = Mod10Dac(digits[11..21]);
            if (dac2 < 0) return (false, "Bloco 2 da linha digitável inválido.");
            if (dac2 != (digits[21] - '0')) return (false, "Dígito verificador do bloco 2 (linha digitável) não confere (módulo 10).");
            int dac3 = Mod10Dac(digits[22..32]);
            if (dac3 < 0) return (false, "Bloco 3 da linha digitável inválido.");
            if (dac3 != (digits[32] - '0')) return (false, "Dígito verificador do bloco 3 (linha digitável) não confere (módulo 10).");
            var barcode44 = ToBarcode44(digits);
            if (string.IsNullOrEmpty(barcode44)) return (false, "Falha ao derivar código de barras de 44 dígitos.");
            int dacBarcode = Mod11DacBarcode44(barcode44);
            if (dacBarcode < 0) return (false, "Código de barras inválido para módulo 11.");
            if (dacBarcode != (barcode44[4] - '0')) return (false, "Dígito verificador do código de barras não confere (módulo 11).");
            return (true, null);
        }

        /// <summary>
        /// Converte os 47 dígitos da linha digitável no código de barras de 44 dígitos
        /// (remove os 3 dígitos verificadores dos blocos 1, 2 e 3 — posições 10, 21 e 32 em 0-based).
        /// </summary>
        public static string? ToBarcode44(string? digitableLine47Digits)
        {
            var digits = OnlyDigits(digitableLine47Digits);
            if (digits.Length != DigitableLineDigitsLength) return null;
            return digits[..10] + digits[11..21] + digits[22..32] + digits[33..];
        }

        /// <summary>
        /// Formata 47 dígitos no padrão da linha digitável: AAAAA.AAAAC CCCC.CCCCCD DDDD.DDDDDK N NNNNNNNNNNNNN
        /// </summary>
        public static string? FormatDigitableLine47(string? digitableLine47Digits)
        {
            var digits = OnlyDigits(digitableLine47Digits);
            if (digits.Length != DigitableLineDigitsLength) return null;
            return $"{digits[..5]}.{digits[5..10]} {digits[10..11]} {digits[11..16]}.{digits[16..21]} {digits[21..22]} {digits[22..27]}.{digits[27..32]} {digits[32..33]} {digits[33..34]} {digits[34..]}";
        }

        /// <summary>
        /// Normaliza o valor retornado pelo Asaas: retorna (código de barras 44 dígitos, linha digitável formatada).
        /// Se o valor já tiver pontos/espaços, trata como linha digitável; se for só números (47 ou 44), converte.
        /// </summary>
        public static (string? Barcode44, string? DigitableLineFormatted) NormalizeBoletoValues(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return (null, null);
            var trimmed = rawValue.Trim();
            var digits = OnlyDigits(trimmed);

            if (digits.Length == DigitableLineDigitsLength)
            {
                var barcode44 = ToBarcode44(digits);
                var formatted = FormatDigitableLine47(digits);
                return (barcode44, formatted);
            }

            if (digits.Length == BarcodeLength)
            {
                return (digits, trimmed.Contains('.') || trimmed.Contains(' ') ? trimmed : digits);
            }

            if (trimmed.Contains('.') || trimmed.Contains(' '))
            {
                if (digits.Length == DigitableLineDigitsLength)
                {
                    var barcode44 = ToBarcode44(digits);
                    var formatted = FormatDigitableLine47(digits);
                    return (barcode44, formatted);
                }
                return (null, trimmed);
            }

            return (trimmed, trimmed);
        }
    }
}
