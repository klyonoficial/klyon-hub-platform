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

        /// <summary>
        /// Documento de contrato ainda vigente.
        /// Vencido quando <see cref="Document.ExpiresAt"/> tem valor e é anterior a <paramref name="referenceUtc"/>.
        /// Legado sem <c>ExpiresAt</c> é tratado como ainda válido (evita regeneração/duplicata indevida).
        /// </summary>
        public static bool IsContractDocumentValid(Document document, DateTime? referenceUtc = null)
        {
            var now = referenceUtc ?? DateTime.UtcNow;
            if (!document.ExpiresAt.HasValue)
                return true;
            return document.ExpiresAt.Value >= now;
        }

        public static IEnumerable<Document> MatchingContractDocuments(
            Contract contract,
            IEnumerable<Document> documents)
        {
            var kind = ResolveServiceKind(contract.Service?.Code);
            if (kind == null)
                return Enumerable.Empty<Document>();

            var variant = FiscalAddressContractRules.ResolveVariant(contract.Client);
            return documents.Where(d =>
                d.Type == DocumentType.Contract &&
                DocumentMatches(d.Description, kind.Value, variant));
        }

        public static bool CanGenerate(
            Contract contract,
            IEnumerable<Document> documents,
            bool hasPaidPayment,
            DateTime? referenceUtc = null)
        {
            if (!MeetsGenerationPrerequisites(contract, hasPaidPayment, referenceUtc))
                return false;

            return !MatchingContractDocuments(contract, documents).Any();
        }

        /// <summary>
        /// Regeneração manual: só quando já existem documentos do tipo atual e todos estão vencidos.
        /// </summary>
        public static bool CanRegenerate(
            Contract contract,
            IEnumerable<Document> documents,
            bool hasPaidPayment,
            DateTime? referenceUtc = null)
        {
            if (!MeetsGenerationPrerequisites(contract, hasPaidPayment, referenceUtc))
                return false;

            var matching = MatchingContractDocuments(contract, documents).ToList();
            if (matching.Count == 0)
                return false;

            return matching.All(d => !IsContractDocumentValid(d, referenceUtc));
        }

        private static bool MeetsGenerationPrerequisites(
            Contract contract,
            bool hasPaidPayment,
            DateTime? referenceUtc)
        {
            if (ResolveServiceKind(contract.Service?.Code) == null)
                return false;
            if (!hasPaidPayment)
                return false;
            if (!contract.DocumentsApproved)
                return false;
            if (!AddressDeclarationRules.IsContractWithinValidity(contract, referenceUtc))
                return false;
            return true;
        }
    }
}
