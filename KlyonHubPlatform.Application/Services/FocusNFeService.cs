using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KlyonHubPlatform.Application.DTOs.FocusNFe;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace KlyonHubPlatform.Application.Services
{
    public class FocusNFeService : IFocusNFeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FocusNFeService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuditService? _auditService;
        private const string FOCUS_API_HOMOLOGACAO = "https://homologacao.focusnfe.com.br";
        private const string FOCUS_API_PRODUCAO = "https://api.focusnfe.com.br";
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
        public FocusNFeService(
            ApplicationDbContext context,
            ILogger<FocusNFeService> logger,
            IHttpClientFactory httpClientFactory,
            IAuditService? auditService = null)
        {
            _context = context;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _auditService = auditService;
        }
        private string GetBaseUrl(bool isProduction) => isProduction ? FOCUS_API_PRODUCAO : FOCUS_API_HOMOLOGACAO;
        private HttpClient CreateHttpClient(string apiToken, bool isProduction)
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(GetBaseUrl(isProduction));
            var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{apiToken}:"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
        public async Task<FocusNFSeResponse> EmitirNFSeAsync(FocusNFSeRequest request, string apiToken, bool isProduction = false)
        {
            try
            {
                _logger.LogInformation("Emitindo NFSe via Focus NFe. Ref: {Ref}, Ambiente: {Ambiente}", 
                    request.Ref, isProduction ? "Produção" : "Homologação");
                using var client = CreateHttpClient(apiToken, isProduction);
                var json = JsonSerializer.Serialize(request, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"/v2/nfse?ref={request.Ref}", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Resposta Focus NFe: {StatusCode} - {Response}", 
                    response.StatusCode, responseContent);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Erro ao emitir NFSe: {StatusCode} - {Response}", 
                        response.StatusCode, responseContent);
                    return new FocusNFSeResponse
                    {
                        Ref = request.Ref,
                        Status = "erro_autorizacao",
                        Mensagem = $"Erro HTTP {response.StatusCode}: {responseContent}"
                    };
                }
                var result = JsonSerializer.Deserialize<FocusNFSeResponse>(responseContent, JsonOptions);
                return result ?? new FocusNFSeResponse { Ref = request.Ref, Status = "erro", Mensagem = "Resposta vazia" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exceção ao emitir NFSe via Focus NFe");
                return new FocusNFSeResponse
                {
                    Ref = request.Ref,
                    Status = "erro",
                    Mensagem = ex.Message
                };
            }
        }
        public async Task<FocusNFSeConsultaResponse> ConsultarNFSeAsync(string referencia, string apiToken, bool isProduction = false)
        {
            try
            {
                _logger.LogInformation("Consultando NFSe via Focus NFe. Ref: {Ref}", referencia);
                using var client = CreateHttpClient(apiToken, isProduction);
                var response = await client.GetAsync($"/v2/nfse/{referencia}");
                var responseContent = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("NFSe não encontrada ou erro: {StatusCode} - {Response}", 
                        response.StatusCode, responseContent);
                    return new FocusNFSeConsultaResponse
                    {
                        Ref = referencia,
                        Status = "erro",
                        Mensagem = $"Erro HTTP {response.StatusCode}"
                    };
                }
                var result = JsonSerializer.Deserialize<FocusNFSeConsultaResponse>(responseContent, JsonOptions);
                return result ?? new FocusNFSeConsultaResponse { Ref = referencia, Status = "erro" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exceção ao consultar NFSe via Focus NFe");
                return new FocusNFSeConsultaResponse
                {
                    Ref = referencia,
                    Status = "erro",
                    Mensagem = ex.Message
                };
            }
        }
        public async Task<FocusNFSeCancelResponse> CancelarNFSeAsync(string referencia, string justificativa, string apiToken, bool isProduction = false)
        {
            try
            {
                _logger.LogInformation("Cancelando NFSe via Focus NFe. Ref: {Ref}", referencia);
                using var client = CreateHttpClient(apiToken, isProduction);
                var request = new FocusNFSeCancelRequest { Justificativa = justificativa };
                var json = JsonSerializer.Serialize(request, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"/v2/nfse/{referencia}")
                {
                    Content = content
                };
                var response = await client.SendAsync(httpRequest);
                var responseContent = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Erro ao cancelar NFSe: {StatusCode} - {Response}", 
                        response.StatusCode, responseContent);
                    return new FocusNFSeCancelResponse
                    {
                        Status = "erro",
                        Mensagem = $"Erro HTTP {response.StatusCode}: {responseContent}"
                    };
                }
                var result = JsonSerializer.Deserialize<FocusNFSeCancelResponse>(responseContent, JsonOptions);
                return result ?? new FocusNFSeCancelResponse { Status = "processando" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exceção ao cancelar NFSe via Focus NFe");
                return new FocusNFSeCancelResponse
                {
                    Status = "erro",
                    Mensagem = ex.Message
                };
            }
        }
        public async Task<FocusXmlResponse> BaixarXmlNFSeAsync(string referencia, string apiToken, bool isProduction = false)
        {
            try
            {
                _logger.LogInformation("Baixando XML da NFSe via Focus NFe. Ref: {Ref}", referencia);
                using var client = CreateHttpClient(apiToken, isProduction);
                var response = await client.GetAsync($"/v2/nfse/{referencia}.xml");
                if (!response.IsSuccessStatusCode)
                {
                    return new FocusXmlResponse
                    {
                        Success = false,
                        ErrorMessage = $"Erro HTTP {response.StatusCode}"
                    };
                }
                var xmlContent = await response.Content.ReadAsStringAsync();
                return new FocusXmlResponse
                {
                    Success = true,
                    XmlContent = xmlContent
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exceção ao baixar XML da NFSe via Focus NFe");
                return new FocusXmlResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        public async Task<bool> ConfigurarWebhookAsync(FocusWebhookConfig config, string apiToken, bool isProduction = false)
        {
            try
            {
                _logger.LogInformation("Configurando webhook no Focus NFe");
                using var client = CreateHttpClient(apiToken, isProduction);
                var json = JsonSerializer.Serialize(config, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/v2/hooks", content);
                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erro ao configurar webhook: {StatusCode} - {Response}", 
                        response.StatusCode, responseContent);
                    return false;
                }
                _logger.LogInformation("Webhook configurado com sucesso");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exceção ao configurar webhook no Focus NFe");
                return false;
            }
        }
        public async Task<FocusNFeWebhookLog> ProcessarWebhookAsync(FocusWebhookPayload payload, string? sourceIp)
        {
            var webhookLog = new FocusNFeWebhookLog
            {
                Ref = payload.Ref ?? string.Empty,
                CnpjEmitente = payload.CnpjEmitente ?? string.Empty,
                DocumentType = payload.Tipo ?? "NFSe",
                Status = payload.Status ?? string.Empty,
                Evento = payload.Evento,
                Numero = payload.Numero,
                CodigoVerificacao = payload.CodigoVerificacao,
                Url = payload.Url,
                CaminhoXml = payload.CaminhoXmlNotaFiscal,
                SourceIp = sourceIp,
                RawPayload = JsonSerializer.Serialize(payload),
                ReceivedAt = DateTime.UtcNow,
                ProcessingStatus = FocusWebhookProcessingStatus.Received
            };
            try
            {
                _logger.LogInformation("Processando webhook Focus NFe. Ref: {Ref}, Status: {Status}, Evento: {Evento}", 
                    payload.Ref, payload.Status, payload.Evento);
                var existingLog = await _context.Set<FocusNFeWebhookLog>()
                    .FirstOrDefaultAsync(w => w.Ref == payload.Ref && w.Status == payload.Status && w.Evento == payload.Evento);
                if (existingLog != null && existingLog.ProcessingStatus == FocusWebhookProcessingStatus.Processed)
                {
                    _logger.LogWarning("Webhook já processado anteriormente. Ref: {Ref}", payload.Ref);
                    webhookLog.ProcessingStatus = FocusWebhookProcessingStatus.Ignored;
                    webhookLog.ErrorMessage = "Webhook duplicado";
                    await SaveWebhookLog(webhookLog);
                    return webhookLog;
                }
                webhookLog.ProcessingStatus = FocusWebhookProcessingStatus.Processing;
                webhookLog.ProcessingAttempts++;
                var fiscalNote = await _context.FiscalNotes
                    .FirstOrDefaultAsync(fn => fn.IdempotencyKey == payload.Ref || fn.AccessKey == payload.Ref);
                if (fiscalNote != null)
                {
                    webhookLog.FiscalNoteId = fiscalNote.Id;
                    await AtualizarStatusNotaFiscal(fiscalNote, payload);
                }
                else
                {
                    _logger.LogWarning("Nota fiscal não encontrada para ref: {Ref}", payload.Ref);
                }
                webhookLog.ProcessingStatus = FocusWebhookProcessingStatus.Processed;
                webhookLog.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar webhook Focus NFe");
                webhookLog.ProcessingStatus = FocusWebhookProcessingStatus.Error;
                webhookLog.ErrorMessage = ex.Message;
            }
            await SaveWebhookLog(webhookLog);
            return webhookLog;
        }
        private async Task AtualizarStatusNotaFiscal(FiscalNote fiscalNote, FocusWebhookPayload payload)
        {
            var statusAnterior = fiscalNote.Status;
            switch (payload.Status?.ToLower())
            {
                case "autorizado":
                    fiscalNote.Status = FiscalNoteStatus.Emitida;
                    fiscalNote.Number = payload.Numero;
                    fiscalNote.VerificationCode = payload.CodigoVerificacao;
                    fiscalNote.ViewUrl = payload.Url;
                    fiscalNote.AuthorizedAt = DateTime.UtcNow;
                    fiscalNote.AuthorizationProtocol = $"FOCUS-{payload.Numero}";
                    if (!string.IsNullOrEmpty(payload.CaminhoXmlNotaFiscal))
                    {
                        fiscalNote.PdfUrl = payload.CaminhoXmlNotaFiscal.Replace(".xml", ".pdf");
                    }
                    break;
                case "cancelado":
                    fiscalNote.Status = FiscalNoteStatus.Cancelada;
                    fiscalNote.CancelledAt = DateTime.UtcNow;
                    break;
                case "erro_autorizacao":
                    fiscalNote.Status = FiscalNoteStatus.Erro;
                    if (payload.Erros?.Any() == true)
                    {
                        fiscalNote.ErrorMessage = string.Join("; ", payload.Erros.Select(e => e.Mensagem));
                    }
                    break;
                case "processando_autorizacao":
                    fiscalNote.Status = FiscalNoteStatus.Processando;
                    break;
            }
            fiscalNote.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Status da nota fiscal atualizado. ID: {Id}, Status anterior: {StatusAnterior}, Novo status: {NovoStatus}", 
                fiscalNote.Id, statusAnterior, fiscalNote.Status);
        }
        private async Task SaveWebhookLog(FocusNFeWebhookLog log)
        {
            _context.Set<FocusNFeWebhookLog>().Add(log);
            await _context.SaveChangesAsync();
        }
        public async Task<FiscalNote> EmitirNotaIntermediacao(FiscalNote fiscalNote, SefazConfiguration config)
        {
            try
            {
                _logger.LogInformation("Emitindo nota de intermediação via Focus NFe. FiscalNoteId: {Id}", fiscalNote.Id);
                if (string.IsNullOrEmpty(config.ApiToken))
                {
                    throw new InvalidOperationException("Token da API Focus NFe não configurado na plataforma.");
                }
                var client = await _context.Clients
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == fiscalNote.ClientId);
                var coworking = await _context.Coworkings
                    .FirstOrDefaultAsync(c => c.Id == fiscalNote.CoworkingId);
                if (coworking == null)
                {
                    throw new InvalidOperationException("Parceiro não encontrado.");
                }
                var referencia = $"INT-{fiscalNote.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                fiscalNote.IdempotencyKey = referencia;
                var request = new FocusNFSeRequest
                {
                    Ref = referencia,
                    DataEmissao = DateTime.Now.ToString("yyyy-MM-dd"),
                    NaturezaOperacao = "1",
                    OptanteSimplesNacional = config.TaxRegime == "Simples Nacional",
                    Prestador = new FocusPrestador
                    {
                        Cnpj = LimparDocumento(config.PlatformCNPJ),
                        InscricaoMunicipal = config.PlatformMunicipalRegistration,
                        CodigoMunicipio = ObterCodigoMunicipioIBGE(config.ISSMunicipality)
                    },
                    Tomador = new FocusTomador
                    {
                        Cnpj = LimparDocumento(coworking.CNPJ),
                        RazaoSocial = coworking.Name,
                        Email = coworking.ContactEmail,
                        Telefone = coworking.ContactPhone,
                        Endereco = !string.IsNullOrEmpty(coworking.Address) ? new FocusEndereco
                        {
                            Logradouro = coworking.Address,
                            Numero = coworking.AddressNumber ?? "S/N",
                            Complemento = coworking.AddressComplement,
                            Bairro = coworking.AddressNeighborhood ?? "Centro",
                            Uf = coworking.AddressState ?? config.State,
                            Cep = LimparCep(coworking.AddressZipCode),
                            CodigoMunicipio = ObterCodigoMunicipioIBGE(coworking.AddressCity)
                        } : null
                    },
                    Servico = new FocusServico
                    {
                        ValorServicos = fiscalNote.CommissionAmount ?? fiscalNote.Amount,
                        BaseCalculo = fiscalNote.CommissionAmount ?? fiscalNote.Amount,
                        ValorLiquido = fiscalNote.CommissionAmount ?? fiscalNote.Amount,
                        Aliquota = config.ISSAliquot,
                        ValorIss = Math.Round((fiscalNote.CommissionAmount ?? fiscalNote.Amount) * (config.ISSAliquot / 100m), 2),
                        IssRetido = false,
                        ItemListaServico = config.IntermediationServiceCode,
                        Discriminacao = $"{config.IntermediationServiceDescription}. Contrato/Reserva #{fiscalNote.ContractId ?? fiscalNote.ReservationId}",
                        CodigoMunicipio = ObterCodigoMunicipioIBGE(config.ISSMunicipality),
                        CodigoCnae = config.PlatformCNAE
                    }
                };
                var isProduction = config.Environment == "Producao";
                var response = await EmitirNFSeAsync(request, config.ApiToken, isProduction);
                if (response.Status == "autorizado" || response.Status == "processando_autorizacao")
                {
                    fiscalNote.Status = response.Status == "autorizado" ? FiscalNoteStatus.Emitida : FiscalNoteStatus.Processando;
                    fiscalNote.Number = response.Numero;
                    fiscalNote.VerificationCode = response.CodigoVerificacao;
                    fiscalNote.ViewUrl = response.Url;
                    fiscalNote.AccessKey = referencia;
                    if (response.Status == "autorizado")
                    {
                        fiscalNote.AuthorizedAt = DateTime.UtcNow;
                        fiscalNote.AuthorizationProtocol = $"FOCUS-{response.Numero}";
                    }
                }
                else
                {
                    fiscalNote.Status = FiscalNoteStatus.Erro;
                    fiscalNote.ErrorMessage = response.Mensagem ?? 
                        (response.Erros?.Any() == true ? string.Join("; ", response.Erros.Select(e => e.Mensagem)) : "Erro desconhecido");
                }
                fiscalNote.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Nota de intermediação processada. ID: {Id}, Status: {Status}", 
                    fiscalNote.Id, fiscalNote.Status);
                return fiscalNote;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao emitir nota de intermediação via Focus NFe");
                fiscalNote.Status = FiscalNoteStatus.Erro;
                fiscalNote.ErrorMessage = ex.Message;
                fiscalNote.UpdatedAt = DateTime.UtcNow;
                throw;
            }
        }
        public async Task<FiscalNote> EmitirNotaServico(FiscalNote fiscalNote, CoworkingSefazConfiguration config)
        {
            try
            {
                _logger.LogInformation("Emitindo nota de serviço via Focus NFe. FiscalNoteId: {Id}", fiscalNote.Id);
                if (string.IsNullOrEmpty(config.ApiToken))
                {
                    throw new InvalidOperationException("Token da API Focus NFe não configurado para o parceiro.");
                }
                var client = await _context.Clients
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == fiscalNote.ClientId);
                if (client == null)
                {
                    throw new InvalidOperationException("Cliente não encontrado.");
                }
                var referencia = $"SRV-{fiscalNote.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}";
                fiscalNote.IdempotencyKey = referencia;
                var request = new FocusNFSeRequest
                {
                    Ref = referencia,
                    DataEmissao = DateTime.Now.ToString("yyyy-MM-dd"),
                    NaturezaOperacao = "1",
                    OptanteSimplesNacional = config.TaxRegime == "Simples Nacional",
                    Prestador = new FocusPrestador
                    {
                        Cnpj = LimparDocumento(config.PartnerCNPJ),
                        InscricaoMunicipal = config.PartnerMunicipalRegistration,
                        CodigoMunicipio = ObterCodigoMunicipioIBGE(config.ISSMunicipality)
                    },
                    Tomador = new FocusTomador
                    {
                        Cnpj = !string.IsNullOrEmpty(client.CNPJ) ? LimparDocumento(client.CNPJ) : null,
                        Cpf = string.IsNullOrEmpty(client.CNPJ) && !string.IsNullOrEmpty(client.CPF) ? LimparDocumento(client.CPF) : null,
                        RazaoSocial = client.RazaoSocial ?? client.NomeFantasia ?? client.User?.FullName ?? "Cliente",
                        Email = client.ContactEmail ?? client.User?.Email,
                        Telefone = client.ContactPhone,
                        Endereco = !string.IsNullOrEmpty(client.Address) ? new FocusEndereco
                        {
                            Logradouro = client.Address,
                            Numero = "S/N",
                            Bairro = "Centro",
                            Uf = client.State ?? config.State,
                            Cep = LimparCep(client.ZipCode),
                            CodigoMunicipio = ObterCodigoMunicipioIBGE(client.City)
                        } : null
                    },
                    Servico = new FocusServico
                    {
                        ValorServicos = fiscalNote.ServiceAmount ?? fiscalNote.Amount,
                        BaseCalculo = fiscalNote.ServiceAmount ?? fiscalNote.Amount,
                        ValorLiquido = fiscalNote.ServiceAmount ?? fiscalNote.Amount,
                        Aliquota = config.ISSAliquot,
                        ValorIss = Math.Round((fiscalNote.ServiceAmount ?? fiscalNote.Amount) * (config.ISSAliquot / 100m), 2),
                        IssRetido = false,
                        ItemListaServico = config.ServiceCode,
                        Discriminacao = $"{config.ServiceDescription}. Contrato/Reserva #{fiscalNote.ContractId ?? fiscalNote.ReservationId}",
                        CodigoMunicipio = ObterCodigoMunicipioIBGE(config.ISSMunicipality),
                        CodigoCnae = config.PartnerCNAE
                    }
                };
                var isProduction = config.Environment == "Producao";
                var response = await EmitirNFSeAsync(request, config.ApiToken, isProduction);
                if (response.Status == "autorizado" || response.Status == "processando_autorizacao")
                {
                    fiscalNote.Status = response.Status == "autorizado" ? FiscalNoteStatus.Emitida : FiscalNoteStatus.Processando;
                    fiscalNote.Number = response.Numero;
                    fiscalNote.VerificationCode = response.CodigoVerificacao;
                    fiscalNote.ViewUrl = response.Url;
                    fiscalNote.AccessKey = referencia;
                    if (response.Status == "autorizado")
                    {
                        fiscalNote.AuthorizedAt = DateTime.UtcNow;
                        fiscalNote.AuthorizationProtocol = $"FOCUS-{response.Numero}";
                    }
                }
                else
                {
                    fiscalNote.Status = FiscalNoteStatus.Erro;
                    fiscalNote.ErrorMessage = response.Mensagem ?? 
                        (response.Erros?.Any() == true ? string.Join("; ", response.Erros.Select(e => e.Mensagem)) : "Erro desconhecido");
                }
                fiscalNote.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Nota de serviço processada. ID: {Id}, Status: {Status}", 
                    fiscalNote.Id, fiscalNote.Status);
                return fiscalNote;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao emitir nota de serviço via Focus NFe");
                fiscalNote.Status = FiscalNoteStatus.Erro;
                fiscalNote.ErrorMessage = ex.Message;
                fiscalNote.UpdatedAt = DateTime.UtcNow;
                throw;
            }
        }
        public async Task<bool> VerificarDisponibilidadeAsync(string apiToken, bool isProduction = false)
        {
            try
            {
                using var client = CreateHttpClient(apiToken, isProduction);
                var response = await client.GetAsync("/v2/");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<FocusNFeWebhookListResult> ListarWebhooksAsync(int page, int pageSize, string? status, string? referencia, CancellationToken cancellationToken = default)
        {
            var query = _context.Set<FocusNFeWebhookLog>().AsQueryable();
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<FocusWebhookProcessingStatus>(status, true, out var processingStatus))
                query = query.Where(w => w.ProcessingStatus == processingStatus);
            if (!string.IsNullOrEmpty(referencia))
                query = query.Where(w => w.Ref.Contains(referencia));
            var total = await query.CountAsync(cancellationToken);
            var logs = await query
                .OrderByDescending(w => w.ReceivedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(w => new FocusNFeWebhookLogItemDto
                {
                    Id = w.Id,
                    Ref = w.Ref,
                    CnpjEmitente = w.CnpjEmitente,
                    DocumentType = w.DocumentType,
                    Status = w.Status,
                    Evento = w.Evento,
                    Numero = w.Numero,
                    CodigoVerificacao = w.CodigoVerificacao,
                    Url = w.Url,
                    FiscalNoteId = w.FiscalNoteId,
                    ProcessingStatus = w.ProcessingStatus.ToString(),
                    ErrorMessage = w.ErrorMessage,
                    SourceIp = w.SourceIp,
                    ReceivedAt = w.ReceivedAt,
                    ProcessedAt = w.ProcessedAt,
                    ProcessingAttempts = w.ProcessingAttempts
                })
                .ToListAsync(cancellationToken);
            return new FocusNFeWebhookListResult
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Logs = logs
            };
        }

        public async Task<FocusNFeConsultaConfigResult?> ObterConfiguracaoParaConsultaAsync(string referencia, CancellationToken cancellationToken = default)
        {
            var fiscalNote = await _context.FiscalNotes
                .FirstOrDefaultAsync(fn => fn.IdempotencyKey == referencia || fn.AccessKey == referencia, cancellationToken);
            if (fiscalNote == null)
                return null;
            string? apiToken = null;
            bool isProduction = false;
            if (fiscalNote.Type == FiscalNoteType.Intermediacao)
            {
                var config = await _context.SefazConfigurations
                    .Where(c => c.IsActive)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (config != null)
                {
                    apiToken = config.ApiToken;
                    isProduction = config.Environment == "Producao";
                }
            }
            else
            {
                var config = await _context.CoworkingSefazConfigurations
                    .Where(c => c.CoworkingId == fiscalNote.CoworkingId && c.IsActive)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (config != null)
                {
                    apiToken = config.ApiToken;
                    isProduction = config.Environment == "Producao";
                }
            }
            return new FocusNFeConsultaConfigResult { ApiToken = apiToken, IsProduction = isProduction };
        }

        public async Task<SefazConfiguration?> ObterSefazConfigAtivaAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SefazConfigurations
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        #region Métodos Auxiliares
        private static string LimparDocumento(string? documento)
        {
            if (string.IsNullOrEmpty(documento)) return string.Empty;
            return documento.Replace(".", "").Replace("/", "").Replace("-", "").Trim();
        }
        private static string LimparCep(string? cep)
        {
            if (string.IsNullOrEmpty(cep)) return string.Empty;
            return cep.Replace("-", "").Replace(".", "").Trim();
        }
        private string ObterCodigoMunicipioIBGE(string? municipio)
        {
            if (string.IsNullOrEmpty(municipio)) return "3205309";
            var municipios = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Vitória", "3205309" },
                { "Vitoria", "3205309" },
                { "Vila Velha", "3205200" },
                { "Serra", "3205002" },
                { "Cariacica", "3201308" },
                { "Cachoeiro de Itapemirim", "3201209" },
                { "Guarapari", "3202405" },
                { "Colatina", "3201506" },
                { "Linhares", "3203205" },
                { "São Mateus", "3204906" },
                { "Aracruz", "3200607" }
            };
            foreach (var (nome, codigo) in municipios)
            {
                if (municipio.Contains(nome, StringComparison.OrdinalIgnoreCase))
                {
                    return codigo;
                }
            }
            return "3205309";
        }
        #endregion
    }
}