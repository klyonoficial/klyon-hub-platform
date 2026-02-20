using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface ISefazService
    {
        bool IsStateSupported(string state);
        Task<SefazConfiguration?> GetConfigurationByStateAsync(string state, string environment = "Producao");
        Task<CoworkingSefazConfiguration?> GetCoworkingConfigurationAsync(int coworkingId, string state, string environment = "Producao");
        Task<FiscalNote> IssueIntermediationNoteAsync(FiscalNote fiscalNote, SefazConfiguration config);
        Task<FiscalNote> IssueServiceNoteAsync(FiscalNote fiscalNote, CoworkingSefazConfiguration config);
        Task<bool> CancelFiscalNoteAsync(int fiscalNoteId, string cancellationReason);
        Task<FiscalNote> GetFiscalNoteStatusAsync(string accessKey);
    }
}