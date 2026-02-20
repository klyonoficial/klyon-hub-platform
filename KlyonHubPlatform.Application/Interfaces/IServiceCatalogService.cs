using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IServiceCatalogService
    {
        Task<List<Service>> GetActiveServicesForClientAsync(int userId, string marketplaceRoute, string chooseLocationRoute);
    }
}