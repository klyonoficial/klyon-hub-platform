using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Application.DTOs.Admin;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAdminService
    {
        Task ResetUserPasswordAsync(int userId, string newPassword, string confirmNewPassword);
        Task VerifyUserEmailAsync(int userId);
        Task<List<User>> GetUsersFilteredAsync(string? search = null, string? role = null, string? status = null, string? emailVerified = null, int page = 1, int pageSize = 15);
        Task<int> GetUsersFilteredCountAsync(string? search = null, string? role = null, string? status = null, string? emailVerified = null);
        Task<List<string>> GetAvailableRolesAsync();
        Task<List<Coworking>> GetCoworkingsFilteredAsync(string? search = null, string? status = null, string? isPartner = null, string? approvalStatus = null, int page = 1, int pageSize = 15);
        Task<int> GetCoworkingsFilteredCountAsync(string? search = null, string? status = null, string? isPartner = null, string? approvalStatus = null);
        Task<List<Contract>> GetContractsFilteredAsync(string? search = null, string? status = null, int? clientId = null, int page = 1, int pageSize = 15);
        Task<int> GetContractsFilteredCountAsync(string? search = null, string? status = null, int? clientId = null);
        Task<List<Client>> GetAllClientsAsync();
        Task<List<CardPriceRequestListItemDto>> GetPendingCardPriceRequestsAsync(CancellationToken cancellationToken = default);
        Task<CardPriceRequestDetailDto?> GetCardPriceRequestDetailAsync(int id, CancellationToken cancellationToken = default);
        Task FixUserPasswordByEmailAsync(string email, string newPassword);
        Task<(Dictionary<int, int> ServicesCount, Dictionary<int, bool> HasPendingPercentages, Dictionary<int, bool> HasMaxCapacity)> GetCoworkingListStatsAsync(IEnumerable<int> coworkingIds, bool filterCapacityAlert, CancellationToken cancellationToken = default);
        Task<Coworking?> GetCoworkingWithDetailsForViewAsync(int id, CancellationToken cancellationToken = default);
        Task<List<Service>> GetServicesForAdminCoworkingViewAsync(bool showCoworkingMenus, CancellationToken cancellationToken = default);
        Task<List<PartnerService>> GetPartnerServicesForCoworkingAsync(int coworkingId, bool showCoworkingMenus, CancellationToken cancellationToken = default);
    }
}