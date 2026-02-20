namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Resultado para a tela de detalhes do dashboard do cliente (reservas, pendências, certificado).
    /// </summary>
    public record ClientDashboardDetailsResult(
        KlyonHubPlatform.Domain.Entities.Client? Client,
        int ReservationsCount,
        bool HasFinancialPending,
        bool HasCertificateExpiring);

    /// <summary>
    /// Serviço que concentra a lógica de dados do dashboard do cliente (evita lógica pesada no controller).
    /// </summary>
    public interface IClientDashboardService
    {
        /// <summary>
        /// Obtém os dados necessários para exibir a view Dashboard do cliente (contratos, reservas, pendências).
        /// </summary>
        Task<ClientDashboardDetailsResult> GetDashboardDetailsAsync(int userId, CancellationToken cancellationToken = default);
    }
}
