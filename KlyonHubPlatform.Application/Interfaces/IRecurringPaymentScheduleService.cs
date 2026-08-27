using KlyonHubPlatform.Application.DTOs.Payments;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IRecurringPaymentScheduleService
    {
        Task<List<PredictedPaymentDto>> GetPredictedNextPaymentsAsync(int clientId, CancellationToken cancellationToken = default);
    }
}
