using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface ICoworkingService
    {
        Task<Coworking?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<Coworking>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<List<Coworking>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<Coworking> CreateAsync(Coworking coworking, CancellationToken cancellationToken = default);
        Task<Coworking> UpdateAsync(Coworking coworking, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
        Task<List<CoworkingAddress>> GetAvailableAddressesAsync(int? locationId = null, CancellationToken cancellationToken = default);
        Task<CoworkingAddress> CreateAddressAsync(CoworkingAddress address, CancellationToken cancellationToken = default);
        Task<CoworkingAddress> UpdateAddressAsync(CoworkingAddress address, CancellationToken cancellationToken = default);
    }
}