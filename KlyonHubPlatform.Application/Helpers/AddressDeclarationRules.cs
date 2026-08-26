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
        /// Declaração associada ao tipo de serviço atual do contrato (independente de vigência).
        /// Legado: registros sem ServiceId contam apenas para contrato fiscal.
        /// </summary>
        public static bool DeclarationMatchesCurrentService(
            AddressDeclaration declaration,
            int? contractServiceId,
            string? contractServiceCode)
        {
            if (declaration.ServiceId.HasValue)
                return declaration.ServiceId == contractServiceId;

            return ServiceCodes.IsFiscalAddress(contractServiceCode);
        }

        /// <summary>
        /// Declaração vigente para o tipo de serviço atual do contrato.
        /// Vencida/inválida quando <see cref="AddressDeclaration.IsValid"/> é false
        /// ou <see cref="AddressDeclaration.EndDate"/> &lt; referência.
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

            return DeclarationMatchesCurrentService(declaration, contractServiceId, contractServiceCode);
        }

        /// <summary>
        /// Primeira geração (background / Declarations): só se ainda não houver declaração do tipo atual.
        /// </summary>
        public static bool CanGenerateDeclaration(
            Contract contract,
            IEnumerable<AddressDeclaration> declarations,
            bool hasPaidPayment,
            DateTime? referenceUtc = null)
        {
            if (!MeetsGenerationPrerequisites(contract, hasPaidPayment, referenceUtc))
                return false;

            var hasAnyForCurrentService = declarations.Any(d =>
                DeclarationMatchesCurrentService(d, contract.ServiceId, contract.Service?.Code));

            return !hasAnyForCurrentService;
        }

        /// <summary>
        /// Regeneração manual: só quando já existem declarações do tipo atual e todas estão vencidas/inválidas.
        /// </summary>
        public static bool CanRegenerateDeclaration(
            Contract contract,
            IEnumerable<AddressDeclaration> declarations,
            bool hasPaidPayment,
            DateTime? referenceUtc = null)
        {
            if (!MeetsGenerationPrerequisites(contract, hasPaidPayment, referenceUtc))
                return false;

            var related = declarations
                .Where(d => DeclarationMatchesCurrentService(d, contract.ServiceId, contract.Service?.Code))
                .ToList();
            if (related.Count == 0)
                return false;

            var hasValidForCurrentService = related.Any(d =>
                IsDeclarationValidForCurrentService(d, contract.ServiceId, contract.Service?.Code, referenceUtc));

            return !hasValidForCurrentService;
        }

        private static bool MeetsGenerationPrerequisites(
            Contract contract,
            bool hasPaidPayment,
            DateTime? referenceUtc)
        {
            if (!hasPaidPayment)
                return false;
            if (!contract.DocumentsApproved)
                return false;
            if (!IsContractWithinValidity(contract, referenceUtc))
                return false;
            return true;
        }
    }
}
