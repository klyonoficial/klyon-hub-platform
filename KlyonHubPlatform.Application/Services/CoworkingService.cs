using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Repositories;
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

        public async Task<Coworking?> GetByIdAsync(int id)
        {
            return await _coworkingRepository.GetByIdAsync(id);
        }

        public async Task<List<Coworking>> GetAllAsync()
        {
            return await _coworkingRepository.GetAllAsync();
        }

        public async Task<List<Coworking>> GetByUserIdAsync(int userId)
        {
            return await _coworkingRepository.GetByUserIdAsync(userId);
        }

        public async Task<Coworking> CreateAsync(Coworking coworking)
        {
            return await _coworkingRepository.CreateAsync(coworking);
        }

        public async Task<Coworking> UpdateAsync(Coworking coworking)
        {
            return await _coworkingRepository.UpdateAsync(coworking);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _coworkingRepository.DeleteAsync(id);
        }

        public async Task<List<CoworkingAddress>> GetAvailableAddressesAsync(int? locationId = null)
        {
            return await _coworkingRepository.GetAvailableAddressesAsync(locationId);
        }

        public async Task<CoworkingAddress> CreateAddressAsync(CoworkingAddress address)
        {
            return await _coworkingRepository.CreateAddressAsync(address);
        }

        public async Task<CoworkingAddress> UpdateAddressAsync(CoworkingAddress address)
        {
            return await _coworkingRepository.UpdateAddressAsync(address);
        }
    }
}
