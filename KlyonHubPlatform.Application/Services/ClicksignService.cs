using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using Microsoft.Extensions.Logging;
namespace KlyonHubPlatform.Application.Services
{
    public class ClicksignService : IClicksignService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };
        private readonly ISystemConfigurationService _config;
        private readonly ILogger<ClicksignService> _logger;
        private readonly HttpClient _httpClient;
        public ClicksignService(
            ISystemConfigurationService config,
            ILogger<ClicksignService> logger,
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
                new MediaTypeWithQualityHeaderValue(ClicksignApiMediaTypes.JsonApi));
        }
        public async Task<bool> IsConfiguredAsync()
        {
            var token = await _config.GetValueAsync(ClicksignConfigKeys.AccessToken);
            return !string.IsNullOrWhiteSpace(token);
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
                _logger.LogDebug("Clicksign não configurado. Contrato não será enviado para assinatura eletrônica.");
                return null;
            }
            var token = await _config.GetValueAsync(ClicksignConfigKeys.AccessToken);
            var environment = await GetEnvironmentAsync();
            var baseUrl = environment.GetBaseUrl();
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token!.Trim());
            _logger.LogDebug("Clicksign usando ambiente {Environment} ({BaseUrl}).", environment, baseUrl);
            try
            {
                var envelopeId = await CreateEnvelopeAsync(baseUrl, fileName);
                if (string.IsNullOrEmpty(envelopeId))
                {
                    _logger.LogWarning("Clicksign: criação de envelope retornou sem ID.");
                    return null;
                }
                var documentId = await UploadDocumentAsync(baseUrl, envelopeId, pdfBytes, fileName);
                if (string.IsNullOrEmpty(documentId))
                {
                    _logger.LogWarning("Clicksign: upload de documento retornou sem ID para envelope {EnvelopeId}.", envelopeId);
                    return null;
                }
                var signerId = await AddSignerAsync(baseUrl, envelopeId, signerEmail, signerName);
                if (string.IsNullOrEmpty(signerId))
                {
                    _logger.LogWarning("Clicksign: adição de signatário retornou sem ID para envelope {EnvelopeId}.", envelopeId);
                    return null;
                }
                await AddQualificationRequirementAsync(baseUrl, envelopeId, documentId, signerId);
                await AddAuthenticationRequirementAsync(baseUrl, envelopeId, documentId, signerId);
                var activated = await ActivateEnvelopeAsync(baseUrl, envelopeId);
                if (!activated)
                {
                    _logger.LogWarning("Clicksign: falha ao ativar envelope {EnvelopeId}.", envelopeId);
                    return null;
                }
                await NotifySignerAsync(baseUrl, envelopeId, signerId, message);
                _logger.LogInformation(
                    "Clicksign: contrato enviado para assinatura. EnvelopeId={EnvelopeId}, Signatário={Email}, Ambiente={Environment}.",
                    envelopeId, signerEmail, environment);
                return envelopeId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Clicksign: erro ao enviar contrato para assinatura. Signatário={Email}.", signerEmail);
                return null;
            }
        }
        private async Task<ClicksignEnvironment> GetEnvironmentAsync()
        {
            var useSandbox = await _config.GetValueAsync(ClicksignConfigKeys.UseSandbox);
            if (string.IsNullOrWhiteSpace(useSandbox))
                return ClicksignEnvironment.Sandbox;
            return StringBooleanValue.IsTrue(useSandbox)
                ? ClicksignEnvironment.Sandbox
                : ClicksignEnvironment.Production;
        }
        private async Task<string?> CreateEnvelopeAsync(string baseUrl, string envelopeName)
        {
            var url = $"{baseUrl}/{ClicksignApiEndpoints.Envelopes}";
            var body = new
            {
                data = new
                {
                    type = "envelopes",
                    attributes = new Dictionary<string, object?>
                    {
                        ["name"] = envelopeName
                    }
                }
            };
            var json = JsonSerializer.Serialize(body, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, ClicksignApiMediaTypes.JsonApi);
            var response = await _httpClient.PostAsync(url, content);
            var responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Clicksign create envelope falhou. Status={StatusCode}, Response={Response}", response.StatusCode, responseText);
                return null;
            }
            return GetDataIdFromJson(responseText);
        }
        private async Task<string?> UploadDocumentAsync(string baseUrl, string envelopeId, byte[] pdfBytes, string fileName)
        {
            var url = $"{baseUrl}/{ClicksignApiEndpoints.Envelopes}/{envelopeId}/{ClicksignApiEndpoints.Documents}";
            var base64 = Convert.ToBase64String(pdfBytes);
            var body = new
            {
                data = new
                {
                    type = "documents",
                    attributes = new Dictionary<string, object?>
                    {
                        ["file"] = base64,
                        ["name"] = fileName
                    }
                }
            };
            var json = JsonSerializer.Serialize(body, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, ClicksignApiMediaTypes.JsonApi);
            var response = await _httpClient.PostAsync(url, content);
            var responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Clicksign upload document falhou. Status={StatusCode}, Response={Response}", response.StatusCode, responseText);
                return null;
            }
            return GetDataIdFromJson(responseText);
        }
        private async Task<string?> AddSignerAsync(string baseUrl, string envelopeId, string email, string? name)
        {
            var url = $"{baseUrl}/{ClicksignApiEndpoints.Envelopes}/{envelopeId}/{ClicksignApiEndpoints.Signers}";
            var attrs = new Dictionary<string, object?> { ["email"] = email };
            if (!string.IsNullOrWhiteSpace(name))
                attrs["name"] = name;
            var body = new
            {
                data = new
                {
                    type = "signers",
                    attributes = attrs
                }
            };
            var json = JsonSerializer.Serialize(body, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, ClicksignApiMediaTypes.JsonApi);
            var response = await _httpClient.PostAsync(url, content);
            var responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Clicksign add signer falhou. Status={StatusCode}, Response={Response}", response.StatusCode, responseText);
                return null;
            }
            return GetDataIdFromJson(responseText);
        }
        private async Task AddQualificationRequirementAsync(string baseUrl, string envelopeId, string documentId, string signerId)
        {
            var url = $"{baseUrl}/{ClicksignApiEndpoints.Envelopes}/{envelopeId}/{ClicksignApiEndpoints.Documents}/{documentId}/{ClicksignApiEndpoints.RequirementsQualification}";
            var body = new
            {
                data = new
                {
                    type = "requirements",
                    attributes = new Dictionary<string, object?>(),
                    relationships = new
                    {
                        signer = new { data = new { type = "signers", id = signerId } }
                    }
                }
            };
            var json = JsonSerializer.Serialize(body, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, ClicksignApiMediaTypes.JsonApi);
            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
                _logger.LogDebug("Clicksign qualification requirement (pode ser opcional). Status={StatusCode}", response.StatusCode);
        }
        private async Task AddAuthenticationRequirementAsync(string baseUrl, string envelopeId, string documentId, string signerId)
        {
            var url = $"{baseUrl}/{ClicksignApiEndpoints.Envelopes}/{envelopeId}/{ClicksignApiEndpoints.Documents}/{documentId}/{ClicksignApiEndpoints.RequirementsAuthentication}";
            var body = new
            {
                data = new
                {
                    type = "requirements",
                    attributes = new Dictionary<string, object?>(),
                    relationships = new
                    {
                        signer = new { data = new { type = "signers", id = signerId } }
                    }
                }
            };
            var json = JsonSerializer.Serialize(body, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, ClicksignApiMediaTypes.JsonApi);
            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
                _logger.LogDebug("Clicksign authentication requirement (pode ser opcional). Status={StatusCode}", response.StatusCode);
        }
        private async Task<bool> ActivateEnvelopeAsync(string baseUrl, string envelopeId)
        {
            var url = $"{baseUrl}/{ClicksignApiEndpoints.Envelopes}/{envelopeId}";
            var body = new
            {
                data = new
                {
                    type = "envelopes",
                    id = envelopeId,
                    attributes = new Dictionary<string, object?>
                    {
                        ["status"] = ClicksignEnvelopeStatus.Running.ToApiValue()
                    }
                }
            };
            var json = JsonSerializer.Serialize(body, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, ClicksignApiMediaTypes.JsonApi);
            using var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Clicksign activate envelope falhou. Status={StatusCode}, Response={Response}", response.StatusCode, responseText);
                return false;
            }
            return true;
        }
        private async Task NotifySignerAsync(string baseUrl, string envelopeId, string signerId, string? message)
        {
            var url = $"{baseUrl}/{ClicksignApiEndpoints.Envelopes}/{envelopeId}/{ClicksignApiEndpoints.Signers}/{signerId}/{ClicksignApiEndpoints.Notify}";
            var body = new
            {
                data = new
                {
                    type = "notifications",
                    attributes = new Dictionary<string, object?>
                    {
                        ["message"] = message ?? "Por favor, assine o documento."
                    }
                }
            };
            var json = JsonSerializer.Serialize(body, JsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, ClicksignApiMediaTypes.JsonApi);
            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
                _logger.LogDebug("Clicksign notify signer. Status={StatusCode}", response.StatusCode);
        }
        private static string? GetDataIdFromJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var idProp))
                    return idProp.GetString();
            }
            catch
            {
            }
            return null;
        }
    }
}