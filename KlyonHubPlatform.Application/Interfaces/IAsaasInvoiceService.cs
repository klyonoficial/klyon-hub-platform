using KlyonHubPlatform.Application.DTOs.Asaas;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço de nota fiscal via API Asaas (agendar, emitir, listar, cancelar).
    /// Ref: https://docs.asaas.com/reference/agendar-nota-fiscal
    /// </summary>
    public interface IAsaasInvoiceService
    {
        /// <summary>
        /// Agenda uma nota fiscal para uma cobrança.
        /// </summary>
        /// <param name="paymentId">ID da cobrança no Asaas</param>
        /// <param name="customerId">ID do cliente no Asaas</param>
        /// <param name="serviceDescription">Descrição do serviço</param>
        /// <param name="observations">Observações</param>
        /// <param name="externalReference">Referência externa (ex: ID do Payment ou Contract no sistema)</param>
        Task<AsaasInvoiceResponse?> ScheduleInvoiceAsync(
            string paymentId,
            string customerId,
            string? serviceDescription = null,
            string? observations = null,
            string? externalReference = null);

        /// <summary>
        /// Recupera uma nota fiscal pelo ID.
        /// </summary>
        Task<AsaasInvoiceResponse?> GetInvoiceAsync(string invoiceId);

        /// <summary>
        /// Lista notas fiscais (filtros opcionais).
        /// </summary>
        Task<List<AsaasInvoiceResponse>> ListInvoicesAsync(string? paymentId = null, string? customerId = null, int limit = 10, int offset = 0);

        /// <summary>
        /// Emite uma nota fiscal previamente agendada.
        /// </summary>
        Task<AsaasInvoiceResponse?> IssueInvoiceAsync(string invoiceId);

        /// <summary>
        /// Cancela uma nota fiscal.
        /// </summary>
        Task<bool> CancelInvoiceAsync(string invoiceId);
    }
}
