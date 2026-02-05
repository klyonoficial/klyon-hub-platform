using System.Text;
using System.Text.Json;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    /// <summary>
    /// Integração com a API D4Sign para assinatura eletrônica de contratos.
    /// Documentação: https://docapi.d4sign.com.br/docs/introdução-a-api
    /// Para testes use ambiente Sandbox (D4Sign.UseSandbox = true).
    /// </summary>
    public class D4SignService : ID4SignService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly ISystemConfigurationService _config;
        private readonly ILogger<D4SignService> _logger;
        private readonly HttpClient _httpClient;

        public D4SignService(
            ISystemConfigurationService config,
            ILogger<D4SignService> logger,
            HttpClient httpClient)
        {
            _config = config;
            _logger = logger;
            _httpClient = httpClient;
            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<bool> IsConfiguredAsync()
        {
            var token = await _config.GetValueAsync(D4SignConfigKeys.TokenAPI);
            var safeUuid = await _config.GetValueAsync(D4SignConfigKeys.SafeUuid);
            return !string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(safeUuid);
        }

        public async Task<string?> SendContractForSignatureAsync(
            byte[] pdfBytes,
            string fileName,
            string signerEmail,
            string? signerName = null,
            string? message = null)
        {
            if (!await IsConfiguredAsync())
            {
                _logger.LogDebug("D4Sign não configurado. Contrato não será enviado para assinatura eletrônica.");
                return null;
            }

            var token = await _config.GetValueAsync(D4SignConfigKeys.TokenAPI);
            var cryptKey = await _config.GetValueAsync(D4SignConfigKeys.CryptKey);
            var safeUuid = await _config.GetValueAsync(D4SignConfigKeys.SafeUuid);
            var environment = await GetEnvironmentAsync();
            var baseUrl = environment.GetBaseUrl();
            var authQuery = BuildAuthQuery(token!, cryptKey);

            _logger.LogDebug("D4Sign usando ambiente {Environment} ({BaseUrl}).", environment, baseUrl);

            try
            {
                var documentUuid = await UploadDocumentAsync(baseUrl, authQuery, safeUuid!, pdfBytes, fileName);
                if (string.IsNullOrEmpty(documentUuid))
                {
                    _logger.LogWarning("D4Sign: upload retornou sem UUID do documento.");
                    return null;
                }

                var signerOk = await CreateSignersAsync(baseUrl, authQuery, documentUuid, signerEmail, signerName);
                if (!signerOk)
                {
                    _logger.LogWarning("D4Sign: falha ao cadastrar signatário para documento {DocumentUuid}.", documentUuid);
                    return null;
                }

                var sendOk = await SendToSignerAsync(
                    baseUrl,
                    authQuery,
                    documentUuid,
                    message,
                    D4SignSkipEmail.SendEmail,
                    D4SignWorkflow.Sequential);
                if (!sendOk)
                {
                    _logger.LogWarning("D4Sign: falha ao enviar documento {DocumentUuid} para assinatura.", documentUuid);
                    return null;
                }

                _logger.LogInformation(
                    "D4Sign: contrato enviado para assinatura. DocumentUuid={DocumentUuid}, Signatário={Email}, Ambiente={Environment}.",
                    documentUuid, signerEmail, environment);
                return documentUuid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "D4Sign: erro ao enviar contrato para assinatura. Signatário={Email}.", signerEmail);
                return null;
            }
        }

        /// <summary>
        /// Obtém o ambiente configurado. Se não definido ou inválido, assume Sandbox para segurança em testes.
        /// </summary>
        private async Task<D4SignEnvironment> GetEnvironmentAsync()
        {
            var useSandbox = await _config.GetValueAsync(D4SignConfigKeys.UseSandbox);
            if (string.IsNullOrWhiteSpace(useSandbox))
                return D4SignEnvironment.Sandbox;
            return useSandbox.Trim().ToLowerInvariant() == "true"
                ? D4SignEnvironment.Sandbox
                : D4SignEnvironment.Production;
        }

        private static string BuildAuthQuery(string token, string? cryptKey)
        {
            var q = $"tokenAPI={Uri.EscapeDataString(token)}";
            if (!string.IsNullOrWhiteSpace(cryptKey))
                q += $"&cryptKey={Uri.EscapeDataString(cryptKey)}";
            return q;
        }

        private async Task<string?> UploadDocumentAsync(string baseUrl, string authQuery, string safeUuid, byte[] pdfBytes, string fileName)
        {
            var url = $"{baseUrl}/documents/{safeUuid}/uploadbinary?{authQuery}";
            var base64 = Convert.ToBase64String(pdfBytes);
            var body = new Dictionary<string, string>
            {
                ["base64_binary_file"] = base64,
                ["mime_type"] = "application/pdf",
                ["name"] = fileName
            };
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("D4Sign upload falhou. Status={StatusCode}, Response={Response}", response.StatusCode, responseText);
                return null;
            }

            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;
            if (root.TryGetProperty("uuid", out var uuidProp))
                return uuidProp.GetString();
            return null;
        }

        private async Task<bool> CreateSignersAsync(string baseUrl, string authQuery, string documentUuid, string email, string? name)
        {
            var url = $"{baseUrl}/documents/{documentUuid}/createlist?{authQuery}";
            var signers = new[]
            {
                new Dictionary<string, object>
                {
                    ["email"] = email,
                    ["act"] = D4SignSignerAct.Sign.ToApiValue(),
                    ["foreign"] = D4SignSignerForeign.National.ToApiValue(),
                    ["certificadoicpbr"] = D4SignCertificadoIcpBr.No.ToApiValue(),
                    ["assinatura_presencial"] = D4SignAssinaturaPresencial.No.ToApiValue(),
                    ["docauth"] = D4SignDocAuth.No.ToApiValue(),
                    ["docauthandselfie"] = D4SignDocAuthAndSelfie.No.ToApiValue(),
                    ["embed_methodauth"] = D4SignEmbedMethodAuth.Email.ToApiValue(),
                    ["upload_allow"] = D4SignUploadAllow.No.ToApiValue()
                }
            };
            var body = new { signers };
            var json = JsonSerializer.Serialize(body, JsonOptions);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("D4Sign createlist falhou. Status={StatusCode}, Response={Response}", response.StatusCode, responseText);
                return false;
            }
            return true;
        }

        private async Task<bool> SendToSignerAsync(
            string baseUrl,
            string authQuery,
            string documentUuid,
            string? message,
            D4SignSkipEmail skipEmail,
            D4SignWorkflow workflow)
        {
            var url = $"{baseUrl}/documents/{documentUuid}/sendtosigner?{authQuery}";
            var body = new Dictionary<string, string>
            {
                ["skip_email"] = skipEmail.ToApiValue(),
                ["workflow"] = workflow.ToApiValue()
            };
            if (!string.IsNullOrWhiteSpace(message))
                body["message"] = message;

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("D4Sign sendtosigner falhou. Status={StatusCode}, Response={Response}", response.StatusCode, responseText);
                return false;
            }
            return true;
        }
    }
}
