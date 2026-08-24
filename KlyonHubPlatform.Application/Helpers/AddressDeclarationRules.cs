using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Helpers
{
    public static class AddressDeclarationRules
    {
        public static bool IsContractWithinValidity(Contract contract, DateTime? referenceUtc = null)
        {
            var now = referenceUtc ?? DateTime.UtcNow;
            if (contract.Status != ContractStatus.Active && contract.Status != ContractStatus.PendingCancellation)
                return false;
            if (!contract.ExpiryDate.HasValue)
                return false;
            return contract.ExpiryDate.Value >= now;
        }

        /// <summary>
        /// Declaração vigente para o tipo de serviço atual do contrato.
        /// Legado: registros sem ServiceId contam apenas para contrato fiscal.
        /// </summary>
        public static bool IsDeclarationValidForCurrentService(
            AddressDeclaration declaration,
            int? contractServiceId,
            string? contractServiceCode,
            DateTime? referenceUtc = null)
        {
            var now = referenceUtc ?? DateTime.UtcNow;
            if (!declaration.IsValid || declaration.EndDate < now)
                return false;

            if (declaration.ServiceId.HasValue)
                return declaration.ServiceId == contractServiceId;

            return ServiceCodes.IsFiscalAddress(contractServiceCode);
        }

        public static bool CanGenerateDeclaration(
            Contract contract,
            IEnumerable<AddressDeclaration> declarations,
            bool hasPaidPayment,
            DateTime? referenceUtc = null)
        {
            if (!hasPaidPayment)
                return false;
            if (!IsContractWithinValidity(contract, referenceUtc))
                return false;

            var hasValidForCurrentService = declarations.Any(d =>
                IsDeclarationValidForCurrentService(d, contract.ServiceId, contract.Service?.Code, referenceUtc));

            return !hasValidForCurrentService;
        }
    }
}
