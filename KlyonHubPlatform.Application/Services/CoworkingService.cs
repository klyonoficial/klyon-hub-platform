using System.Threading;
using System.Threading.Tasks;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Interfaces;

namespace KlyonHubPlatform.Application.Services
{
    public class CoworkingService : ICoworkingService
    {
        private readonly ICoworkingRepository _coworkingRepository;

        public CoworkingService(ICoworkingRepository coworkingRepository)
        {
            _coworkingRepository = coworkingRepository;
        }

        public async Task<Coworking?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _coworkingRepository.GetByIdAsync(id, cancellationToken);
        }

        public async Task<List<Coworking>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _coworkingRepository.GetAllAsync(cancellationToken);
        }

        public async Task<List<Coworking>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            return await _coworkingRepository.GetByUserIdAsync(userId, cancellationToken);
        }

        public async Task<Coworking> CreateAsync(Coworking coworking, CancellationToken cancellationToken = default)
        {
            return await _coworkingRepository.CreateAsync(coworking, cancellationToken);
        }

        public async Task<Coworking> UpdateAsync(Coworking coworking, CancellationToken cancellationToken = default)
        {
            return await _coworkingRepository.UpdateAsync(coworking, cancellationToken);
        }

        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _coworkingRepository.DeleteAsync(id, cancellationToken);
        }

        public async Task<List<CoworkingAddress>> GetAvailableAddressesAsync(int? locationId = null, CancellationToken cancellationToken = default)
        {
            return await _coworkingRepository.GetAvailableAddressesAsync(locationId, cancellationToken);
        }

        public async Task<CoworkingAddress> CreateAddressAsync(CoworkingAddress address, CancellationToken cancellationToken = default)
        {
            return await _coworkingRepository.CreateAddressAsync(address, cancellationToken);
        }

        public async Task<CoworkingAddress> UpdateAddressAsync(CoworkingAddress address, CancellationToken cancellationToken = default)
        {
            return await _coworkingRepository.UpdateAddressAsync(address, cancellationToken);
        }
    }
}
