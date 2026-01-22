using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KlyonHubPlatform.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    public class D4SignService : ID4SignService
    {
        private const string ProductionBaseUrl = "https://secure.d4sign.com.br/api/v1";
        private const string SandboxBaseUrl = "https://sandbox.d4sign.com.br/api/v1";

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
        }

        public async Task<bool> IsConfiguredAsync()
        {
            var token = await _config.GetValueAsync("D4Sign.TokenAPI");
            var safeUuid = await _config.GetValueAsync("D4Sign.SafeUuid");
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

            var token = await _config.GetValueAsync("D4Sign.TokenAPI");
            var cryptKey = await _config.GetValueAsync("D4Sign.CryptKey");
            var safeUuid = await _config.GetValueAsync("D4Sign.SafeUuid");
            var baseUrl = await GetBaseUrlAsync();
            var authQuery = BuildAuthQuery(token!, cryptKey);

            try
            {
                // 1. Upload do documento (uploadbinary)
                var documentUuid = await UploadDocumentAsync(baseUrl, authQuery, safeUuid!, pdfBytes, fileName);
                if (string.IsNullOrEmpty(documentUuid))
                {
                    _logger.LogWarning("D4Sign: upload retornou sem UUID do documento.");
                    return null;
                }

                // 2. Cadastrar signatário (createlist)
                var signerOk = await CreateSignersAsync(baseUrl, authQuery, documentUuid, signerEmail, signerName);
                if (!signerOk)
                {
                    _logger.LogWarning("D4Sign: falha ao cadastrar signatário para documento {DocumentUuid}.", documentUuid);
                    return null;
                }

                // 3. Enviar para assinatura (sendtosigner) — skip_email 0 para enviar e-mail ao signatário
                var sendOk = await SendToSignerAsync(baseUrl, authQuery, documentUuid, message, skipEmail: "0", workflow: "0");
                if (!sendOk)
                {
                    _logger.LogWarning("D4Sign: falha ao enviar documento {DocumentUuid} para assinatura.", documentUuid);
                    return null;
                }

                _logger.LogInformation("D4Sign: contrato enviado para assinatura. DocumentUuid={DocumentUuid}, Signatário={Email}.", documentUuid, signerEmail);
                return documentUuid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "D4Sign: erro ao enviar contrato para assinatura. Signatário={Email}.", signerEmail);
                return null;
            }
        }

        private async Task<string> GetBaseUrlAsync()
        {
            var useSandbox = await _config.GetValueAsync("D4Sign.UseSandbox");
            return useSandbox?.ToLowerInvariant() == "true" ? SandboxBaseUrl : ProductionBaseUrl;
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
                    ["act"] = "1",
                    ["foreign"] = "0",
                    ["certificadoicpbr"] = "0",
                    ["assinatura_presencial"] = "0",
                    ["docauth"] = "0",
                    ["docauthandselfie"] = "0",
                    ["embed_methodauth"] = "email",
                    ["upload_allow"] = "0"
                }
            };
            var body = new { signers };
            var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
            var json = JsonSerializer.Serialize(body, opts);

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

        private async Task<bool> SendToSignerAsync(string baseUrl, string authQuery, string documentUuid, string? message, string skipEmail, string workflow)
        {
            var url = $"{baseUrl}/documents/{documentUuid}/sendtosigner?{authQuery}";
            var body = new Dictionary<string, string>
            {
                ["skip_email"] = skipEmail,
                ["workflow"] = workflow
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
