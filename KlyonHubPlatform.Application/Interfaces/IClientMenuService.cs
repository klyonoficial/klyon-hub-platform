namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Resultado da verificação de cadastro completo do cliente (CPF/CNPJ, nome, telefone, estado).
    /// TempDataMessageKey: chave para o controller mapear para InfoMessages (ex: "CompleteRegistration", "CompleteRegistrationWithFields").
    /// </summary>
    public record ClientRegistrationStatus(bool IsComplete, string? TempDataMessageKey, string? RedirectActionName);

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

        /// <summary>
        /// Verifica se o cadastro do cliente está completo (CPF ou CNPJ, nome, telefone, estado).
        /// Retorna status para o controller redirecionar e exibir mensagem quando incompleto.
        /// </summary>
        Task<ClientRegistrationStatus> GetRegistrationStatusAsync(int userId);
    }
}
