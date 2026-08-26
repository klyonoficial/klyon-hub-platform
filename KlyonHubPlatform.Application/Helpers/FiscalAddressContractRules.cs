using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Helpers
{
    public enum FiscalAddressContractVariant
    {
        WithoutCnpj,
        WithCnpj
    }

    public static class FiscalAddressContractRules
    {
        public const string DescriptionWithCnpj = "Contrato Endereço Fiscal (com CNPJ)";
        public const string DescriptionWithoutCnpj = "Contrato Endereço Fiscal (reserva, sem CNPJ)";

        public static FiscalAddressContractVariant ResolveVariant(Client? client)
        {
            if (client == null)
                return FiscalAddressContractVariant.WithoutCnpj;

            if (PdfFieldFormatter.HasCnpj(client.CNPJ))
                return FiscalAddressContractVariant.WithCnpj;

            if (client.User?.ClientHasCnpj == true)
                return FiscalAddressContractVariant.WithCnpj;

            return FiscalAddressContractVariant.WithoutCnpj;
        }

        public static FiscalAddressContractVariant ResolveVariant(string? clientCnpj) =>
            PdfFieldFormatter.HasCnpj(clientCnpj)
                ? FiscalAddressContractVariant.WithCnpj
                : FiscalAddressContractVariant.WithoutCnpj;

        public static string DescriptionForVariant(FiscalAddressContractVariant variant) =>
            variant == FiscalAddressContractVariant.WithCnpj
                ? DescriptionWithCnpj
                : DescriptionWithoutCnpj;

        public static bool DocumentMatchesVariant(string? description, FiscalAddressContractVariant variant) =>
            string.Equals(description?.Trim(), DescriptionForVariant(variant), StringComparison.OrdinalIgnoreCase);

        public static bool CanGenerateFiscalContract(
            Contract contract,
            IEnumerable<Document> documents,
            bool hasPaidPayment,
            DateTime? referenceUtc = null)
        {
            if (!ServiceCodes.IsFiscalAddress(contract.Service?.Code))
                return false;

            return AddressContractRules.CanGenerate(contract, documents, hasPaidPayment, referenceUtc);
        }
    }
}
