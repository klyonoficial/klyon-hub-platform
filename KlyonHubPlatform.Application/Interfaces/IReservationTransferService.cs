using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IReservationTransferService
    {
        Task ExecuteTransferIfBothConfirmedAsync(Reservation reservation);
    }
}