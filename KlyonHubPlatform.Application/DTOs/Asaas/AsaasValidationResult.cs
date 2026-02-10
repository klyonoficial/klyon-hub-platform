namespace KlyonHubPlatform.Application.DTOs.Asaas
{
    /// <summary>
    /// Resultado da validação de dados para cobrança Asaas (conforme documentação).
    /// Ref: https://docs.asaas.com/reference/criar-nova-cobranca e https://docs.asaas.com/docs/como-informar-os-dados-do-cliente
    /// </summary>
    public class AsaasValidationResult
    {
        public bool IsValid { get; set; }
        public int? HttpStatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
    }
}
