using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.DTOs.Asaas;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace KlyonHubPlatform.Application.Services
{
    /// <summary>
    /// Integração com API de Notas Fiscais do Asaas.
    /// Ref: https://docs.asaas.com/reference/agendar-nota-fiscal
    /// </summary>
    public class AsaasInvoiceService : IAsaasInvoiceService
    {
        private const string SandboxBaseUrl = "https://api-sandbox.asaas.com/v3";
        private const string ProductionBaseUrl = "https://api.asaas.com/v3";

        private readonly ISystemConfigurationService _systemConfig;
        private readonly ILogger<AsaasInvoiceService> _logger;
        private readonly HttpClient _httpClient;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public AsaasInvoiceService(
            ISystemConfigurationService systemConfig,
            ILogger<AsaasInvoiceService> logger,
            HttpClient httpClient)
        {
            _systemConfig = systemConfig;
            _logger = logger;
            _httpClient = httpClient;
        }

        private async Task<string> GetBaseUrlAsync()
        {
            var useSandbox = await _systemConfig.GetValueAsync("Asaas.UseSandbox");
            return (useSandbox?.ToLower() == "true") ? SandboxBaseUrl : ProductionBaseUrl;
        }

        private async Task<string> GetApiKeyAsync()
        {
            var key = await _systemConfig.GetValueAsync("Asaas.ApiKey");
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("API Key Asaas não configurada. Configure Asaas.ApiKey nas Configurações do Sistema.");
            return key;
        }

        private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, object? body = null)
        {
            var apiKey = await GetApiKeyAsync();
            var baseUrl = await GetBaseUrlAsync();
            var request = new HttpRequestMessage(method, $"{baseUrl}{path}");
            request.Headers.Add("access_token", apiKey);
            request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("User-Agent", "KlyonHubPlatform/1.0");
            if (body != null && (method == HttpMethod.Post || method == HttpMethod.Put))
            {
                request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
            }
            return request;
        }

        public async Task<AsaasInvoiceResponse?> ScheduleInvoiceAsync(
            string paymentId,
            string customerId,
            string? serviceDescription = null,
            string? observations = null,
            string? externalReference = null)
        {
            var req = new AsaasInvoiceRequest
            {
                Payment = paymentId,
                Customer = customerId,
                ServiceDescription = serviceDescription ?? "Serviço de endereço fiscal",
                Observations = observations,
                ExternalReference = externalReference
            };

            var httpRequest = await CreateRequestAsync(HttpMethod.Post, "/invoices", req);
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao agendar nota fiscal Asaas: {StatusCode} - {Content}", response.StatusCode, content);
                return null;
            }

            return JsonSerializer.Deserialize<AsaasInvoiceResponse>(content, JsonOptions);
        }

        public async Task<AsaasInvoiceResponse?> GetInvoiceAsync(string invoiceId)
        {
            var httpRequest = await CreateRequestAsync(HttpMethod.Get, $"/invoices/{invoiceId}");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Erro ao obter nota fiscal Asaas: {StatusCode} - {Content}", response.StatusCode, content);
                return null;
            }

            return JsonSerializer.Deserialize<AsaasInvoiceResponse>(content, JsonOptions);
        }

        public async Task<List<AsaasInvoiceResponse>> ListInvoicesAsync(string? paymentId = null, string? customerId = null, int limit = 10, int offset = 0)
        {
            var query = new List<string> { $"limit={limit}", $"offset={offset}" };
            if (!string.IsNullOrEmpty(paymentId)) query.Add($"payment={paymentId}");
            if (!string.IsNullOrEmpty(customerId)) query.Add($"customer={customerId}");
            var path = "/invoices?" + string.Join("&", query);

            var httpRequest = await CreateRequestAsync(HttpMethod.Get, path);
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Erro ao listar notas fiscais Asaas: {StatusCode} - {Content}", response.StatusCode, content);
                return new List<AsaasInvoiceResponse>();
            }

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return new List<AsaasInvoiceResponse>();

            var list = new List<AsaasInvoiceResponse>();
            foreach (var item in data.EnumerateArray())
            {
                var json = item.GetRawText();
                var inv = JsonSerializer.Deserialize<AsaasInvoiceResponse>(json, JsonOptions);
                if (inv != null) list.Add(inv);
            }
            return list;
        }

        public async Task<AsaasInvoiceResponse?> IssueInvoiceAsync(string invoiceId)
        {
            var httpRequest = await CreateRequestAsync(HttpMethod.Post, $"/invoices/{invoiceId}/authorize");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao emitir nota fiscal Asaas: {StatusCode} - {Content}", response.StatusCode, content);
                return null;
            }

            return JsonSerializer.Deserialize<AsaasInvoiceResponse>(content, JsonOptions);
        }

        public async Task<bool> CancelInvoiceAsync(string invoiceId)
        {
            var httpRequest = await CreateRequestAsync(HttpMethod.Post, $"/invoices/{invoiceId}/cancel");
            var response = await _httpClient.SendAsync(httpRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro ao cancelar nota fiscal Asaas: {StatusCode} - {Content}", response.StatusCode, content);
                return false;
            }

            return true;
        }
    }
}
