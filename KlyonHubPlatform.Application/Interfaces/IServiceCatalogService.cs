using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço que fornece a lista de serviços da plataforma para exibição ao cliente (tela de Serviços).
    /// Centraliza regras de negócio: deduplicação, correção de rotas/descrições, criação de serviços padrão e filtro por feature.
    /// </summary>
    public interface IServiceCatalogService
    {
        /// <summary>
        /// Retorna a lista de serviços ativos para o cliente. Se o cliente tiver contrato ativo, retorna lista vazia.
        /// Aplica deduplicação por nome, correção de descrições/rotas e respeita Features.EnableCoworkingMenus.
        /// </summary>
        /// <param name="userId">Id do usuário (cliente).</param>
        /// <param name="marketplaceRoute">Rota do marketplace (ex: /Home/Marketplace).</param>
        /// <param name="chooseLocationRoute">Rota de escolha de local (ex: /Client/ChooseLocation).</param>
        Task<List<Service>> GetActiveServicesForClientAsync(int userId, string marketplaceRoute, string chooseLocationRoute);
    }
}
