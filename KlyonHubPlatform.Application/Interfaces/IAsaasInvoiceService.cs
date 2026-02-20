using KlyonHubPlatform.Application.DTOs.Asaas;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IAsaasInvoiceService
    {
        Task<AsaasInvoiceResponse?> ScheduleInvoiceAsync(
            string paymentId,
            string customerId,
            string? serviceDescription = null,
            string? observations = null,
            string? externalReference = null);
        Task<AsaasInvoiceResponse?> GetInvoiceAsync(string invoiceId);
        Task<List<AsaasInvoiceResponse>> ListInvoicesAsync(string? paymentId = null, string? customerId = null, int limit = 10, int offset = 0);
        Task<AsaasInvoiceResponse?> IssueInvoiceAsync(string invoiceId);
        Task<bool> CancelInvoiceAsync(string invoiceId);
    }
}