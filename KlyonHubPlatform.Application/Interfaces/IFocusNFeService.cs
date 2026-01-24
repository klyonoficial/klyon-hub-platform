using KlyonHubPlatform.Application.DTOs.FocusNFe;
using KlyonHubPlatform.Domain.Entities;

namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço de integração com Focus NFe para emissão de notas fiscais
    /// Documentação: https://focusnfe.com.br/doc/
    /// </summary>
    public interface IFocusNFeService
    {
        /// <summary>
        /// Emite uma NFSe (Nota Fiscal de Serviço Eletrônica) via Focus NFe
        /// </summary>
        /// <param name="request">Dados da NFSe a ser emitida</param>
        /// <param name="apiToken">Token de autenticação do Focus NFe</param>
        /// <param name="isProduction">Se deve usar ambiente de produção</param>
        /// <returns>Resposta da emissão</returns>
        Task<FocusNFSeResponse> EmitirNFSeAsync(FocusNFSeRequest request, string apiToken, bool isProduction = false);

        /// <summary>
        /// Consulta o status de uma NFSe pelo ID de referência
        /// </summary>
        /// <param name="referencia">ID de referência da nota (nosso ID)</param>
        /// <param name="apiToken">Token de autenticação do Focus NFe</param>
        /// <param name="isProduction">Se deve usar ambiente de produção</param>
        /// <returns>Dados da NFSe</returns>
        Task<FocusNFSeConsultaResponse> ConsultarNFSeAsync(string referencia, string apiToken, bool isProduction = false);

        /// <summary>
        /// Cancela uma NFSe emitida
        /// </summary>
        /// <param name="referencia">ID de referência da nota (nosso ID)</param>
        /// <param name="justificativa">Justificativa do cancelamento</param>
        /// <param name="apiToken">Token de autenticação do Focus NFe</param>
        /// <param name="isProduction">Se deve usar ambiente de produção</param>
        /// <returns>Resposta do cancelamento</returns>
        Task<FocusNFSeCancelResponse> CancelarNFSeAsync(string referencia, string justificativa, string apiToken, bool isProduction = false);

        /// <summary>
        /// Baixa o XML de uma NFSe emitida
        /// </summary>
        /// <param name="referencia">ID de referência da nota (nosso ID)</param>
        /// <param name="apiToken">Token de autenticação do Focus NFe</param>
        /// <param name="isProduction">Se deve usar ambiente de produção</param>
        /// <returns>Conteúdo do XML</returns>
        Task<FocusXmlResponse> BaixarXmlNFSeAsync(string referencia, string apiToken, bool isProduction = false);

        /// <summary>
        /// Configura a URL de webhook para receber notificações
        /// </summary>
        /// <param name="config">Configuração do webhook</param>
        /// <param name="apiToken">Token de autenticação do Focus NFe</param>
        /// <param name="isProduction">Se deve usar ambiente de produção</param>
        Task<bool> ConfigurarWebhookAsync(FocusWebhookConfig config, string apiToken, bool isProduction = false);

        /// <summary>
        /// Processa um webhook recebido do Focus NFe
        /// </summary>
        /// <param name="payload">Payload do webhook</param>
        /// <param name="sourceIp">IP de origem</param>
        /// <returns>Log do webhook processado</returns>
        Task<FocusNFeWebhookLog> ProcessarWebhookAsync(FocusWebhookPayload payload, string? sourceIp);

        /// <summary>
        /// Emite nota fiscal de intermediação (comissão da plataforma)
        /// </summary>
        Task<FiscalNote> EmitirNotaIntermediacao(FiscalNote fiscalNote, SefazConfiguration config);

        /// <summary>
        /// Emite nota fiscal de serviço (parceiro para cliente)
        /// </summary>
        Task<FiscalNote> EmitirNotaServico(FiscalNote fiscalNote, CoworkingSefazConfiguration config);

        /// <summary>
        /// Verifica se a API do Focus NFe está disponível
        /// </summary>
        Task<bool> VerificarDisponibilidadeAsync(string apiToken, bool isProduction = false);
    }
}
