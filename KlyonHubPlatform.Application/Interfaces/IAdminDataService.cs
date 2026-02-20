using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço de acesso a dados para ações administrativas. Concentra operações que antes usavam DbContext diretamente no AdminController.
    /// </summary>
    public interface IAdminDataService
    {
        Task<CoworkingAddress?> GetAddressByIdAndCoworkingAsync(int addressId, int coworkingId, CancellationToken cancellationToken = default);
        Task SetAddressStatusAsync(int addressId, int coworkingId, AddressStatus newStatus, CancellationToken cancellationToken = default);
        Task<Coworking?> GetCoworkingWithUserAsync(int id, CancellationToken cancellationToken = default);
        Task<AuditLog?> GetAuditLogWithUserAsync(int id, CancellationToken cancellationToken = default);
        Task<Coworking?> GetCoworkingForEditAsync(int id, CancellationToken cancellationToken = default);
        Task<Coworking?> FindCoworkingAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> CoworkingExistsCnpjAsync(string cnpjClean, int excludeCoworkingId, CancellationToken cancellationToken = default);
        Task<Service?> GetFiscalAddressServiceAsync(CancellationToken cancellationToken = default);
        Task<PartnerService?> GetPartnerServiceForCoworkingAndServiceAsync(int coworkingId, int serviceId, CancellationToken cancellationToken = default);
        Task<(bool Success, string? ErrorMessage)> ToggleCoworkingStatusAsync(int id, CancellationToken cancellationToken = default);
    }
}
