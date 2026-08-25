using KlyonHubPlatform.Application.DTOs.PagBank;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Helpers;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Helpers
{
    /// <summary>
    /// Dados mínimos enviados ao gateway de pagamento.
    /// Nome legal completo e endereço não são exigidos na criação do pedido — só no PDF (ClientContractPartyRules).
    /// </summary>
    public static class PaymentGatewayCustomer
    {
        public static string ResolveName(Client client)
        {
            var email = ResolveEmail(client);
            foreach (var candidate in new[] { client.RazaoSocial, client.NomeFantasia, client.ContactName, client.User?.FullName })
            {
                var real = BrazilianText.NormalizePersonName(
                    ClientDisplayName.NullIfPlaceholder(candidate, email));
                if (!string.IsNullOrWhiteSpace(real))
                    return real;
            }
            return ClientDisplayName.Resolve(null, email);
        }

        public static string? ResolveEmail(Client client) =>
            BrazilianText.NormalizeEmail(FirstNonBlank(client.ContactEmail, client.User?.Email));

        public static string? ResolvePhone(Client client) =>
            FirstNonBlank(client.ContactPhone, client.User?.Phone);

        /// <summary>
        /// Endereço real do cliente para o gateway. Omite placeholders de checkout (Não informado, 00000-000).
        /// </summary>
        public static PagBankAddress? ResolveAddress(Client client)
        {
            var street = RealText(client.Address);
            var number = RealText(client.AddressNumber);
            var neighborhood = RealText(client.Neighborhood);
            var complement = RealText(client.Complement);
            var city = RealText(client.City);
            var postalCode = RealPostalCode(client.ZipCode);
            var regionCode = RealUf(client.State);

            if (street == null && number == null && neighborhood == null && city == null && postalCode == null)
                return null;

            return new PagBankAddress
            {
                Street = BrazilianText.NormalizePersonName(street) ?? street ?? string.Empty,
                Number = number ?? (street != null ? "S/N" : string.Empty),
                Complement = BrazilianText.NormalizePersonName(complement) ?? complement,
                Locality = BrazilianText.NormalizePersonName(neighborhood) ?? neighborhood ?? string.Empty,
                City = BrazilianText.NormalizePersonName(city) ?? city ?? string.Empty,
                RegionCode = regionCode ?? string.Empty,
                Country = "BRA",
                PostalCode = postalCode ?? string.Empty
            };
        }

        /// <summary>
        /// Endereço do titular do cartão: CEP e número vêm do form (já normalizados); demais campos só se forem reais.
        /// </summary>
        public static PagBankAddress BuildCardHolderAddress(Client client, string postalCode, string addressNumber)
        {
            var existing = ResolveAddress(client);
            return new PagBankAddress
            {
                Street = existing?.Street ?? string.Empty,
                Number = addressNumber,
                Complement = existing?.Complement,
                Locality = existing?.Locality ?? string.Empty,
                City = existing?.City ?? string.Empty,
                RegionCode = existing?.RegionCode ?? string.Empty,
                Country = "BRA",
                PostalCode = postalCode
            };
        }

        public static string? RealAddressNumber(string? value) => RealText(value);

        public static string ResolveTaxIdDigits(Client client)
        {
            var digits = BrazilianTaxId.Digits(FirstNonBlank(client.CNPJ, client.CPF));
            if (string.IsNullOrEmpty(digits))
                return string.Empty;
            if (BrazilianTaxIdPlaceholders.IsPlaceholder(digits) || digits.All(c => c == '0') || digits.All(c => c == '9'))
                return string.Empty;
            return digits;
        }

        public static bool CanCharge(Client client)
        {
            var email = ResolveEmail(client);
            var taxId = ResolveTaxIdDigits(client);
            var name = ResolveName(client);
            return !string.IsNullOrWhiteSpace(name)
                && !string.IsNullOrWhiteSpace(email)
                && email.Contains('@')
                && taxId.Length is 11 or 14;
        }

        private static string? FirstNonBlank(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
            return null;
        }

        private static string? RealText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var trimmed = value.Trim();
            if (string.Equals(trimmed, DefaultDisplayStrings.NaoInformado, StringComparison.OrdinalIgnoreCase))
                return null;
            return trimmed;
        }

        public static string? RealPostalCode(string? zip)
        {
            if (string.IsNullOrWhiteSpace(zip))
                return null;
            var digits = new string(zip.Where(char.IsDigit).ToArray());
            if (digits.Length != 8 || digits.All(c => c == '0'))
                return null;
            return digits;
        }

        private static string? RealUf(string? state)
        {
            var uf = BrazilianText.NormalizeUf(state);
            if (uf == null)
                return null;
            return BrazilianUfs.All.Contains(uf) ? uf : null;
        }
    }
}
