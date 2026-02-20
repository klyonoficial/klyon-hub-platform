namespace KlyonHubPlatform.Application.ViewModels
{
    /// <summary>
    /// Resultado da listagem do dashboard admin (GetDashboardList). Retornado como null quando o tipo é inválido.
    /// </summary>
    public class DashboardListResult
    {
        public List<object> Data { get; set; } = new();
        public int? TotalCount { get; set; }
    }
}
