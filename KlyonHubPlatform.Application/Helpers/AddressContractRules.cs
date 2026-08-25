using KlyonHubPlatform.Domain.Core.Constants;

using KlyonHubPlatform.Domain.Core.Enums;

using KlyonHubPlatform.Domain.Entities;



namespace KlyonHubPlatform.Application.Helpers

{

    public enum AddressContractServiceKind

    {

        Fiscal,

        Commercial

    }



    public static class AddressContractRules

    {

        public const string LegacyFiscalDescription = "Contrato de Endereço Fiscal";



        public static AddressContractServiceKind? ResolveServiceKind(string? serviceCode)

        {

            if (ServiceCodes.IsFiscalAddress(serviceCode))

                return AddressContractServiceKind.Fiscal;

            if (ServiceCodes.IsCommercialAddress(serviceCode))

                return AddressContractServiceKind.Commercial;

            return null;

        }



        public static string DescriptionFor(AddressContractServiceKind kind, FiscalAddressContractVariant variant) =>

            kind switch

            {

                AddressContractServiceKind.Fiscal => FiscalAddressContractRules.DescriptionForVariant(variant),

                AddressContractServiceKind.Commercial => CommercialAddressContractRules.DescriptionForVariant(variant),

                _ => LegacyFiscalDescription

            };



        public static bool DocumentMatches(

            string? description,

            AddressContractServiceKind kind,

            FiscalAddressContractVariant variant)

        {

            if (string.Equals(description?.Trim(), DescriptionFor(kind, variant), StringComparison.OrdinalIgnoreCase))

                return true;



            if (kind == AddressContractServiceKind.Fiscal

                && string.Equals(description?.Trim(), LegacyFiscalDescription, StringComparison.OrdinalIgnoreCase))

                return true;



            return false;

        }



        public static bool CanGenerate(

            Contract contract,

            IEnumerable<Document> documents,

            bool hasPaidPayment,

            DateTime? referenceUtc = null)

        {

            var kind = ResolveServiceKind(contract.Service?.Code);

            if (kind == null)

                return false;

            if (!hasPaidPayment)

                return false;

            if (!contract.DocumentsApproved)

                return false;

            if (!AddressDeclarationRules.IsContractWithinValidity(contract, referenceUtc))

                return false;



            var variant = FiscalAddressContractRules.ResolveVariant(contract.Client);

            var hasMatchingDocument = documents.Any(d =>

                d.Type == DocumentType.Contract &&

                DocumentMatches(d.Description, kind.Value, variant));



            return !hasMatchingDocument;

        }

    }

}

