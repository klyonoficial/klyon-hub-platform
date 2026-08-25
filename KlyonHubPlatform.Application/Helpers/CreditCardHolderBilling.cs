using KlyonHubPlatform.Domain.Core.Constants;

namespace KlyonHubPlatform.Application.Helpers
{
    /// <summary>
    /// CEP e número do titular para Asaas creditCardHolderInfo (obrigatórios no cartão; PIX/boleto não usam).
    /// </summary>
    public static class CreditCardHolderBilling
    {
        public const string NoNumberValue = "S/N";

        public static bool TryNormalize(string? postalCode, string? addressNumber, bool addressHasNoNumber, out string postalCodeDigits, out string normalizedNumber, out string error)
        {
            postalCodeDigits = string.Empty;
            normalizedNumber = string.Empty;
            error = string.Empty;

            var digits = BrazilianTaxId.Digits(postalCode);
            if (digits.Length != 8 || digits.All(c => c == '0'))
            {
                error = "Informe um CEP válido (8 dígitos). O CEP 00000-000 não é aceito.";
                return false;
            }

            if (addressHasNoNumber)
            {
                postalCodeDigits = digits;
                normalizedNumber = NoNumberValue;
                return true;
            }

            var number = (addressNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(number)
                || string.Equals(number, DefaultDisplayStrings.NaoInformado, StringComparison.OrdinalIgnoreCase))
            {
                error = "Informe o número do endereço ou marque \"sem número\".";
                return false;
            }

            if (number.Length > 20)
            {
                error = "Número do endereço muito longo (máximo 20 caracteres).";
                return false;
            }

            postalCodeDigits = digits;
            normalizedNumber = number;
            return true;
        }

        public static (string PostalCode, string AddressNumber) NormalizeOrThrow(string? postalCode, string? addressNumber, bool addressHasNoNumber)
        {
            if (!TryNormalize(postalCode, addressNumber, addressHasNoNumber, out var digits, out var number, out var error))
                throw new InvalidOperationException(error);
            return (digits, number);
        }

        /// <summary>Lê CEP/número já persistidos no cartão (número "S/N" conta como sem número).</summary>
        public static bool TryReadStored(string? postalCode, string? addressNumber, out string postalCodeDigits, out string normalizedNumber)
        {
            postalCodeDigits = string.Empty;
            normalizedNumber = string.Empty;
            var trimmed = (addressNumber ?? string.Empty).Trim();
            var noNumber = string.Equals(trimmed, NoNumberValue, StringComparison.OrdinalIgnoreCase);
            return TryNormalize(postalCode, trimmed, noNumber, out postalCodeDigits, out normalizedNumber, out _);
        }

        public static string? FormatMasked(string? zip)
        {
            var digits = PaymentGatewayCustomer.RealPostalCode(zip);
            if (digits == null)
                return null;
            return $"{digits[..5]}-{digits[5..]}";
        }
    }
}
