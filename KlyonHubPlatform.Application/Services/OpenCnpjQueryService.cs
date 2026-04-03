using System.Globalization;
using System.Net;
using System.Text.Json;
using KlyonHubPlatform.Application.DTOs.OpenCnpj;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Helpers;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    public class OpenCnpjQueryService : IOpenCnpjQueryService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenCnpjQueryService> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public OpenCnpjQueryService(HttpClient httpClient, ILogger<OpenCnpjQueryService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<OpenCnpjValidationResult> ValidateForFiscalOnboardingAsync(string cnpj14, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cnpj14) || cnpj14.Length != 14 || !cnpj14.All(char.IsDigit))
                return OpenCnpjValidationResult.Fail("CNPJ inválido.");

            try
            {
                using var response = await _httpClient.GetAsync(cnpj14, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return OpenCnpjValidationResult.Fail("CNPJ não encontrado na base pública.");

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OpenCNPJ retornou {Status} para CNPJ mascarado.", response.StatusCode);
                    return OpenCnpjValidationResult.Fail("Não foi possível consultar o CNPJ. Tente novamente em instantes.");
                }

                var dto = JsonSerializer.Deserialize<OpenCnpjOrganizationDto>(json, JsonOptions);
                if (dto == null)
                    return OpenCnpjValidationResult.Fail("Resposta da consulta de CNPJ inválida.");

                var situacao = (dto.SituacaoCadastral ?? "").Trim();
                if (string.IsNullOrEmpty(situacao))
                    return OpenCnpjValidationResult.Fail("Situação cadastral do CNPJ não informada na consulta.");

                if (!IsSituacaoAtiva(situacao))
                {
                    var norm = situacao.ToLowerInvariant();
                    if (norm.Contains("baix", StringComparison.Ordinal) || norm.Contains("suspens", StringComparison.Ordinal) ||
                        norm.Contains("inapt", StringComparison.Ordinal) || norm.Contains("nul", StringComparison.Ordinal))
                        return OpenCnpjValidationResult.Fail($"CNPJ com situação cadastral não permitida: {situacao}. É necessário estar ativo na Receita Federal.");

                    return OpenCnpjValidationResult.Fail($"Situação cadastral não permitida para contratação: {situacao}.");
                }

                if (FiscalOnboardingCnaeRules.AnyCnaeBlocked(dto.CnaePrincipal, dto.CnaesSecundarios, out var cnaeReason))
                    return OpenCnpjValidationResult.Fail(cnaeReason);

                return OpenCnpjValidationResult.Ok(dto.RazaoSocial?.Trim(), dto.CnaePrincipal?.Trim());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao consultar OpenCNPJ.");
                return OpenCnpjValidationResult.Fail("Não foi possível validar o CNPJ no momento. Verifique sua conexão e tente novamente.");
            }
        }

        /// <summary>API retorna "Ativa" para empresas ativas; aceita variações com/sem acento.</summary>
        private static bool IsSituacaoAtiva(string situacao)
        {
            var s = situacao.Trim().ToLowerInvariant()
                .Replace("á", "a", StringComparison.Ordinal)
                .Replace("ã", "a", StringComparison.Ordinal);
            return s == "ativa" || s == "ativo";
        }
    }
}
