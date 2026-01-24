using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço de integração com SEFAZ para emissão de notas fiscais
    /// Suporta múltiplos estados, cada um com sua própria configuração
    /// </summary>
    public interface ISefazService
    {
        /// <summary>
        /// Verifica se um estado tem integração SEFAZ implementada
        /// </summary>
        bool IsStateSupported(string state);

        /// <summary>
        /// Obtém a configuração SEFAZ da plataforma para um estado específico
        /// </summary>
        Task<SefazConfiguration?> GetConfigurationByStateAsync(string state, string environment = "Producao");

        /// <summary>
        /// Obtém a configuração SEFAZ do parceiro para um estado específico
        /// </summary>
        Task<CoworkingSefazConfiguration?> GetCoworkingConfigurationAsync(int coworkingId, string state, string environment = "Producao");

        /// <summary>
        /// Emite uma nota fiscal de intermediação (comissão) via SEFAZ do estado
        /// A plataforma emite NF de comissão sobre o valor da comissão
        /// </summary>
        Task<FiscalNote> IssueIntermediationNoteAsync(FiscalNote fiscalNote, SefazConfiguration config);

        /// <summary>
        /// Emite uma nota fiscal de serviço via SEFAZ do estado
        /// O parceiro emite NF de serviço sobre o valor total do serviço prestado
        /// </summary>
        Task<FiscalNote> IssueServiceNoteAsync(FiscalNote fiscalNote, CoworkingSefazConfiguration config);

        /// <summary>
        /// Cancela uma nota fiscal emitida
        /// </summary>
        Task<bool> CancelFiscalNoteAsync(int fiscalNoteId, string cancellationReason);

        /// <summary>
        /// Consulta o status de uma nota fiscal na SEFAZ
        /// </summary>
        Task<FiscalNote> GetFiscalNoteStatusAsync(string accessKey);
    }
}
