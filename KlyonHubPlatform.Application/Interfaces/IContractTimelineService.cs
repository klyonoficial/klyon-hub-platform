using KlyonHubPlatform.Application.DTOs.Contracts;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IContractTimelineService
    {
        Task<ContractTimelinePageDto> BuildPagedAsync(
            Contract contract,
            IReadOnlyList<Payment> payments,
            IReadOnlyDictionary<int, List<PaymentHistory>> historiesByPaymentId,
            IReadOnlyList<AddressDeclaration> declarations,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}
