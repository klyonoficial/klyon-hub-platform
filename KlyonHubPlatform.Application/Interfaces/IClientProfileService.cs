using KlyonHubPlatform.Application.DTOs.ClientProfile;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IClientProfileService
    {
        bool ClientContractPartyIsComplete(Client? client);
        Task<ClientContractPartyStatus> GetContractPartyStatusByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<ClientContractPartyStatus> GetContractPartyStatusByContractIdAsync(int contractId, CancellationToken cancellationToken = default);
        /// <summary>
        /// Ponto único de persistência do perfil do cliente (dados do contratante). O histórico do contrato é gravado em <see cref="IClientAppService.SaveClientProfileAsync"/>, que também tenta sincronizar o customer no Asaas se já existir.
        /// </summary>
        Task<ClientProfileSaveResult> SaveProfileAsync(ClientProfileSaveInput input, CancellationToken cancellationToken = default);
    }
}
