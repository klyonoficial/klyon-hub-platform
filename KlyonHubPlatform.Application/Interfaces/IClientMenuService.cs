namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço para regras do menu do cliente (ex.: exibir ou ocultar "Localidades Disponíveis").
    /// Cliente não pode adicionar novo endereço fiscal se já tiver um contrato ativo.
    /// </summary>
    public interface IClientMenuService
    {
        /// <summary>
        /// Verifica se o cliente (por userId) possui ao menos um contrato com status Active.
        /// Equivale a "ter endereço ativo" (ex.: exibido em Meus Contratos).
        /// </summary>
        Task<bool> ClientHasActiveContractAsync(int userId);
    }
}
