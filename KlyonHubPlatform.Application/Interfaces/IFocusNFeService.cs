using KlyonHubPlatform.Application.DTOs.FocusNFe;
using KlyonHubPlatform.Domain.Entities;
namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IFocusNFeService
    {
        Task<FocusNFSeResponse> EmitirNFSeAsync(FocusNFSeRequest request, string apiToken, bool isProduction = false);
        Task<FocusNFSeConsultaResponse> ConsultarNFSeAsync(string referencia, string apiToken, bool isProduction = false);
        Task<FocusNFSeCancelResponse> CancelarNFSeAsync(string referencia, string justificativa, string apiToken, bool isProduction = false);
        Task<FocusXmlResponse> BaixarXmlNFSeAsync(string referencia, string apiToken, bool isProduction = false);
        Task<bool> ConfigurarWebhookAsync(FocusWebhookConfig config, string apiToken, bool isProduction = false);
        Task<FocusNFeWebhookLog> ProcessarWebhookAsync(FocusWebhookPayload payload, string? sourceIp);
        Task<FiscalNote> EmitirNotaIntermediacao(FiscalNote fiscalNote, SefazConfiguration config);
        Task<FiscalNote> EmitirNotaServico(FiscalNote fiscalNote, CoworkingSefazConfiguration config);
        Task<bool> VerificarDisponibilidadeAsync(string apiToken, bool isProduction = false);

        /// <summary>Lista webhooks com paginação e filtros (uso admin).</summary>
        Task<FocusNFeWebhookListResult> ListarWebhooksAsync(int page, int pageSize, string? status, string? referencia, CancellationToken cancellationToken = default);

        /// <summary>Obtém configuração (token/ambiente) para consulta de NFSe por referência. Null = nota não encontrada.</summary>
        Task<FocusNFeConsultaConfigResult?> ObterConfiguracaoParaConsultaAsync(string referencia, CancellationToken cancellationToken = default);

        /// <summary>Obtém a configuração SEFAZ ativa (plataforma) para webhook e status.</summary>
        Task<SefazConfiguration?> ObterSefazConfigAtivaAsync(CancellationToken cancellationToken = default);
    }
}