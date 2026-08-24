using System.Globalization;
using System.Text;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.Helpers
{
    public static class PdfFieldFormatter
    {
        public static string OrDash(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        public static string OnlyDigits(string? value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : new string(value.Where(char.IsDigit).ToArray());

        public static bool HasCnpj(string? cnpj) => OnlyDigits(cnpj).Length == 14;

        public static string FormatContractNumberForPdf(string? contractNumber)
        {
            if (string.IsNullOrWhiteSpace(contractNumber))
                return "-";
            var parts = contractNumber.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 3 && int.TryParse(parts[^1], out var seq))
                return $"{seq:000}/{parts[^2]}";
            return contractNumber;
        }

        public static string BuildClientAddressLine(
            string? address,
            string? number,
            string? neighborhood,
            string? city,
            string? state,
            string? zipCode,
            string? complement = null)
        {
            var parts = new List<string>();
            var street = OrDash(address);
            if (street != "-")
            {
                var line = street;
                if (!string.IsNullOrWhiteSpace(number))
                    line += $", {number.Trim()}";
                if (!string.IsNullOrWhiteSpace(complement))
                    line += $", {complement.Trim()}";
                parts.Add(line);
            }
            var cityLine = new List<string>();
            if (!string.IsNullOrWhiteSpace(neighborhood))
                cityLine.Add(neighborhood.Trim());
            if (!string.IsNullOrWhiteSpace(city))
                cityLine.Add(city.Trim());
            if (!string.IsNullOrWhiteSpace(state))
                cityLine.Add(state.Trim());
            if (cityLine.Count > 0)
                parts.Add(string.Join(", ", cityLine));
            if (!string.IsNullOrWhiteSpace(zipCode))
                parts.Add($"CEP: {zipCode.Trim()}");
            return parts.Count == 0 ? "-" : string.Join(", ", parts);
        }

        public static string BuildCoworkingContractorAddress(
            string? address,
            string? number,
            string? complement,
            string? neighborhood,
            string? city,
            string? state,
            string? zipCode)
        {
            var street = OrDash(address);
            if (street == "-")
                return "-";
            var sb = new StringBuilder(street);
            if (!string.IsNullOrWhiteSpace(number))
                sb.Append($", {number.Trim()}");
            if (!string.IsNullOrWhiteSpace(complement))
                sb.Append($" - {complement.Trim()}");
            if (!string.IsNullOrWhiteSpace(neighborhood))
                sb.Append($" - {neighborhood.Trim()}");
            if (!string.IsNullOrWhiteSpace(city) || !string.IsNullOrWhiteSpace(state))
                sb.Append($", {OrDash(city)}/{OrDash(state)}");
            if (!string.IsNullOrWhiteSpace(zipCode))
                sb.Append($", {zipCode.Trim()}");
            return sb.ToString();
        }

        public static string FormatCurrency(decimal value) =>
            value.ToString("C", new CultureInfo("pt-BR"));

        public static string FormatPaymentClause(decimal monthlyValue, ContractCardPlanType? planType)
        {
            if (planType == ContractCardPlanType.Card1x || planType == ContractCardPlanType.PixAnnual)
                return $"{FormatCurrency(monthlyValue)} (anual), conforme plano contratado.";

            return $"{FormatCurrency(monthlyValue)} (mensal), conforme plano contratado.";
        }
    }
}
