namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço de integração com a API Clicksign (v3 Envelope) para assinatura eletrônica de contratos.
    /// Documentação: https://developers.clicksign.com/docs
    /// Rate limit: https://developers.clicksign.com/docs/limite-de-requisicoes
    /// </summary>
    public interface IClicksignService
    {
        /// <summary>
        /// Verifica se a integração Clicksign está configurada (AccessToken preenchido).
        /// </summary>
        Task<bool> IsConfiguredAsync();

        /// <summary>
        /// Envia o PDF do contrato para a Clicksign: envelope, documento, signatário, requisitos e ativação.
        /// Retorna o identificador do envelope na Clicksign ou null se não configurado/falha.
        /// </summary>
        /// <param name="pdfBytes">Conteúdo do PDF do contrato.</param>
        /// <param name="fileName">Nome do arquivo (ex.: contract_CT-2025-000001_20250126120000.pdf).</param>
        /// <param name="signerEmail">E-mail do signatário (cliente).</param>
        /// <param name="signerName">Nome do signatário (opcional).</param>
        /// <param name="message">Mensagem enviada ao signatário na notificação (opcional).</param>
        Task<string?> SendContractForSignatureAsync(
            byte[] pdfBytes,
            string fileName,
            string signerEmail,
            string? signerName = null,
            string? message = null);
    }
}
