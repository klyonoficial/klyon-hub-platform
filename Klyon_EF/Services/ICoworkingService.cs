using KlyonEF.Models;

namespace KlyonEF.Services
{
    public interface ICoworkingService
    {
        Task<Coworking?> GetByIdAsync(int id);
        Task<List<Coworking>> GetAllAsync();
        Task<List<Coworking>> GetByUserIdAsync(int userId);
        Task<Coworking> CreateAsync(Coworking coworking);
        Task<Coworking> UpdateAsync(Coworking coworking);
        Task<bool> DeleteAsync(int id);
        Task<List<CoworkingAddress>> GetAvailableAddressesAsync(int? locationId = null);
        Task<CoworkingAddress> CreateAddressAsync(CoworkingAddress address);
        Task<CoworkingAddress> UpdateAddressAsync(CoworkingAddress address);
    }
}
