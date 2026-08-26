using KlyonHubPlatform.Application.DTOs.PromoCode;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IPromoCodeService
    {
        Task<PromoCodeApplyResult> ValidateAndApplyAsync(
            string code,
            int coworkingId,
            int addressId,
            PaymentMethod paymentMethod,
            decimal baseAmount,
            CancellationToken cancellationToken = default);

        Task<PromoCode> CreateAsync(PromoCodeCreateInput input, CancellationToken cancellationToken = default);

        Task<PromoCodeDto?> GetByIdAsync(int id, int? coworkingIdFilter, CancellationToken cancellationToken = default);

        Task<List<PromoCodeDto>> ListAsync(int? coworkingIdFilter, bool includeGlobal, CancellationToken cancellationToken = default);

        Task IncrementRedemptionAsync(int promoCodeId, CancellationToken cancellationToken = default);

        /// <summary>Incrementa resgate do cupom uma única vez por pagamento (idempotente para reprocessamento de webhook).</summary>
        Task TryRecordRedemptionForPaymentAsync(int paymentId, int promoCodeId, CancellationToken cancellationToken = default);

        Task DeactivateAsync(int id, int? coworkingIdFilter, CancellationToken cancellationToken = default);
    }
}
