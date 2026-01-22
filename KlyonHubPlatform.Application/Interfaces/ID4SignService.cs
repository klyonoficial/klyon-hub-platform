namespace KlyonHubPlatform.Application.Interfaces
{
    /// <summary>
    /// Serviço de integração com a API D4Sign para assinatura eletrônica de contratos.
    /// Documentação: https://docapi.d4sign.com.br/docs/introdução-a-api
    /// </summary>
    public interface ID4SignService
    {
        /// <summary>
        /// Verifica se a integração D4Sign está configurada (TokenAPI e SafeUuid preenchidos).
        /// </summary>
        Task<bool> IsConfiguredAsync();

        /// <summary>
        /// Envia o PDF do contrato para a D4Sign: upload, cadastro do signatário e envio para assinatura.
        /// Retorna o UUID do documento na D4Sign ou null se não configurado/falha.
        /// </summary>
        /// <param name="pdfBytes">Conteúdo do PDF do contrato.</param>
        /// <param name="fileName">Nome do arquivo (ex.: contract_CT-2025-000001_20250126120000.pdf).</param>
        /// <param name="signerEmail">E-mail do signatário (cliente).</param>
        /// <param name="signerName">Nome do signatário (opcional).</param>
        /// <param name="message">Mensagem enviada ao signatário no e-mail (opcional).</param>
        Task<string?> SendContractForSignatureAsync(
            byte[] pdfBytes,
            string fileName,
            string signerEmail,
            string? signerName = null,
            string? message = null);
    }
}
