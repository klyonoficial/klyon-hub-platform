using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Security.Claims;

namespace KlyonHubPlatform.Application.Services
{
    /// <summary>
    /// Serviço de integração com SEFAZ para emissão de notas fiscais
    /// Suporta múltiplos estados via Focus NFe (intermediário)
    /// </summary>
    public class SefazService : ISefazService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SefazService> _logger;
        private readonly IAuditService? _auditService;
        private readonly IHttpContextAccessor? _httpContextAccessor;
        private readonly IFocusNFeService? _focusNFeService;

        // Estados com integração implementada (via Focus NFe)
        private static readonly HashSet<string> SupportedStates = new HashSet<string> 
        { 
            "ES", "SP", "RJ", "MG", "PR", "SC", "RS", "BA", "PE", "CE", 
            "GO", "DF", "MT", "MS", "PA", "AM", "MA", "RN", "PB", "AL",
            "SE", "PI", "AC", "AP", "RO", "RR", "TO" 
        };

        public SefazService(
            ApplicationDbContext context, 
            ILogger<SefazService> logger, 
            IAuditService? auditService = null, 
            IHttpContextAccessor? httpContextAccessor = null,
            IFocusNFeService? focusNFeService = null)
        {
            _context = context;
            _logger = logger;
            _auditService = auditService;
            _httpContextAccessor = httpContextAccessor;
            _focusNFeService = focusNFeService;
        }

        private async Task LogAuditAsync(int? userId, string action, string entityType, int? entityId, object? oldValues = null, object? newValues = null)
        {
            if (_auditService == null) return;

            string? ipAddress = null;
            string? userAgent = null;
            if (_httpContextAccessor?.HttpContext != null)
            {
                ipAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress?.ToString();
                userAgent = _httpContextAccessor.HttpContext.Request.Headers["User-Agent"].ToString();
            }

            string? oldValuesJson = oldValues != null ? JsonSerializer.Serialize(oldValues) : null;
            string? newValuesJson = newValues != null ? JsonSerializer.Serialize(newValues) : null;

            await _auditService.LogActionAsync(userId, action, entityType, entityId, oldValuesJson, newValuesJson, ipAddress, userAgent);
        }

        private int? GetCurrentUserId()
        {
            if (_httpContextAccessor?.HttpContext?.User != null)
            {
                var userIdClaim = _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
                {
                    return userId;
                }
            }
            return null;
        }

        /// <summary>
        /// Verifica se um estado tem integração implementada
        /// </summary>
        public bool IsStateSupported(string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return false;
            
            return SupportedStates.Contains(state.Trim().ToUpper());
        }

        /// <summary>
        /// Obtém a configuração SEFAZ da plataforma para um estado específico
        /// </summary>
        public async Task<SefazConfiguration?> GetConfigurationByStateAsync(string state, string environment = "Producao")
        {
            if (string.IsNullOrWhiteSpace(state))
                return null;

            var stateUpper = state.Trim().ToUpper();
            
            return await _context.SefazConfigurations
                .Where(c => c.State == stateUpper && c.Environment == environment && c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Obtém a configuração SEFAZ do parceiro para um estado específico
        /// </summary>
        public async Task<CoworkingSefazConfiguration?> GetCoworkingConfigurationAsync(int coworkingId, string state, string environment = "Producao")
        {
            if (string.IsNullOrWhiteSpace(state))
                return null;

            var stateUpper = state.Trim().ToUpper();
            
            return await _context.CoworkingSefazConfigurations
                .Where(c => c.CoworkingId == coworkingId && c.State == stateUpper && c.Environment == environment && c.IsActive)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<FiscalNote> IssueIntermediationNoteAsync(FiscalNote fiscalNote, SefazConfiguration config)
        {
            try
            {
                // Verificar se o estado tem integração implementada
                if (!IsStateSupported(config.State))
                {
                    throw new NotSupportedException($"Integração SEFAZ ainda não implementada para o estado {config.State}. Estados suportados: {string.Join(", ", SupportedStates)}");
                }

                _logger.LogInformation($"Iniciando emissão de NF de intermediação para Reserva #{fiscalNote.ReservationId} ou Contrato #{fiscalNote.ContractId} no estado {config.State}");

                // Validar configuração
                if (!config.IsActive)
                {
                    throw new InvalidOperationException("Configuração da SEFAZ não está ativa.");
                }

                if (string.IsNullOrEmpty(config.PlatformCNPJ))
                {
                    throw new InvalidOperationException("CNPJ da plataforma não configurado.");
                }

                // Se Focus NFe está configurado e disponível, usar ele
                if (_focusNFeService != null && !string.IsNullOrEmpty(config.ApiToken))
                {
                    _logger.LogInformation("Emitindo NF de intermediação via Focus NFe");
                    return await _focusNFeService.EmitirNotaIntermediacao(fiscalNote, config);
                }

                // Fallback: emissão simulada (para testes ou quando Focus NFe não está configurado)
                _logger.LogWarning("Focus NFe não configurado - usando emissão simulada");

                // Determinar URL baseada no ambiente
                var baseUrl = config.Environment == "Producao" 
                    ? config.ProductionUrl 
                    : config.HomologationUrl;

                if (string.IsNullOrEmpty(baseUrl))
                {
                    baseUrl = "https://focusnfe.com.br"; // URL padrão
                }

                // Buscar dados do cliente e parceiro
                var client = await _context.Clients
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == fiscalNote.ClientId);

                var coworking = await _context.Coworkings
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == fiscalNote.CoworkingId);

                if (client == null || coworking == null)
                {
                    throw new InvalidOperationException("Cliente ou parceiro não encontrado.");
                }

                // Simulação de emissão (modo de teste)
                fiscalNote.Number = GenerateFiscalNoteNumber();
                fiscalNote.AccessKey = GenerateAccessKey();
                fiscalNote.VerificationCode = GenerateVerificationCode();
                fiscalNote.Status = FiscalNoteStatus.Emitida;
                fiscalNote.AuthorizedAt = DateTime.UtcNow;
                fiscalNote.AuthorizationProtocol = $"PROT-{DateTime.UtcNow:yyyyMMddHHmmss}";
                fiscalNote.XmlContent = GenerateMockXml(fiscalNote, client, coworking, config);
                fiscalNote.ViewUrl = $"{baseUrl}/nfe/view/{fiscalNote.AccessKey}";
                fiscalNote.PdfUrl = $"{baseUrl}/nfe/pdf/{fiscalNote.AccessKey}";
                fiscalNote.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation($"NF de intermediação emitida com sucesso (simulado): {fiscalNote.AccessKey}");

                // Log de auditoria
                await LogAuditAsync(
                    GetCurrentUserId(),
                    "NF de intermediação emitida (SEFAZ)",
                    "FiscalNote",
                    fiscalNote.Id,
                    null,
                    new { 
                        AccessKey = fiscalNote.AccessKey,
                        Number = fiscalNote.Number,
                        Status = fiscalNote.Status,
                        Amount = fiscalNote.Amount,
                        CommissionAmount = fiscalNote.CommissionAmount,
                        State = config.State,
                        Environment = config.Environment,
                        ReservationId = fiscalNote.ReservationId,
                        ContractId = fiscalNote.ContractId,
                        IntegrationMode = "Simulado"
                    }
                );

                return fiscalNote;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao emitir NF de intermediação: {ex.Message}");
                fiscalNote.Status = FiscalNoteStatus.Erro;
                fiscalNote.ErrorMessage = ex.Message;
                fiscalNote.UpdatedAt = DateTime.UtcNow;
                throw;
            }
        }

        public async Task<bool> CancelFiscalNoteAsync(int fiscalNoteId, string cancellationReason)
        {
            try
            {
                var fiscalNote = await _context.FiscalNotes.FindAsync(fiscalNoteId);
                if (fiscalNote == null)
                {
                    throw new InvalidOperationException("Nota fiscal não encontrada.");
                }

                if (fiscalNote.Status != FiscalNoteStatus.Emitida)
                {
                    throw new InvalidOperationException("Apenas notas fiscais emitidas podem ser canceladas.");
                }

                // TODO: Implementar cancelamento real na SEFAZ ES
                fiscalNote.Status = FiscalNoteStatus.Cancelada;
                fiscalNote.CancelledAt = DateTime.UtcNow;
                fiscalNote.CancellationReason = cancellationReason;
                fiscalNote.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Log de auditoria: cancelamento de NF
                await LogAuditAsync(
                    GetCurrentUserId(),
                    "NF cancelada (SEFAZ)",
                    "FiscalNote",
                    fiscalNoteId,
                    new { Status = nameof(FiscalNoteStatus.Emitida), AccessKey = fiscalNote.AccessKey },
                    new { Status = fiscalNote.Status, CancelledAt = fiscalNote.CancelledAt, CancellationReason = cancellationReason }
                );

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao cancelar NF: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Emite uma nota fiscal de serviço via SEFAZ do estado
        /// O parceiro emite NF de serviço sobre o valor total do serviço prestado
        /// </summary>
        public async Task<FiscalNote> IssueServiceNoteAsync(FiscalNote fiscalNote, CoworkingSefazConfiguration config)
        {
            try
            {
                // Verificar se o estado tem integração implementada
                if (!IsStateSupported(config.State))
                {
                    throw new NotSupportedException($"Integração SEFAZ ainda não implementada para o estado {config.State}. Estados suportados: {string.Join(", ", SupportedStates)}");
                }

                _logger.LogInformation($"Iniciando emissão de NF de serviço para Reserva #{fiscalNote.ReservationId} ou Contrato #{fiscalNote.ContractId} no estado {config.State}");

                // Validar configuração
                if (!config.IsActive)
                {
                    throw new InvalidOperationException("Configuração da SEFAZ do parceiro não está ativa.");
                }

                if (string.IsNullOrEmpty(config.PartnerCNPJ))
                {
                    throw new InvalidOperationException("CNPJ do parceiro não configurado.");
                }

                // Se Focus NFe está configurado e disponível, usar ele
                if (_focusNFeService != null && !string.IsNullOrEmpty(config.ApiToken))
                {
                    _logger.LogInformation("Emitindo NF de serviço via Focus NFe");
                    return await _focusNFeService.EmitirNotaServico(fiscalNote, config);
                }

                // Fallback: emissão simulada (para testes ou quando Focus NFe não está configurado)
                _logger.LogWarning("Focus NFe não configurado para o parceiro - usando emissão simulada");

                // Determinar URL baseada no ambiente
                var baseUrl = config.Environment == "Producao" 
                    ? config.ProductionUrl 
                    : config.HomologationUrl;

                if (string.IsNullOrEmpty(baseUrl))
                {
                    baseUrl = "https://focusnfe.com.br"; // URL padrão
                }

                // Buscar dados do cliente e parceiro
                var client = await _context.Clients
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == fiscalNote.ClientId);

                var coworking = await _context.Coworkings
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.Id == fiscalNote.CoworkingId);

                if (client == null || coworking == null)
                {
                    throw new InvalidOperationException("Cliente ou parceiro não encontrado.");
                }

                // Simulação de emissão (modo de teste)
                fiscalNote.Number = GenerateFiscalNoteNumber();
                fiscalNote.AccessKey = GenerateAccessKeyForPartner(config.PartnerCNPJ, config.State);
                fiscalNote.VerificationCode = GenerateVerificationCode();
                fiscalNote.Status = FiscalNoteStatus.Emitida;
                fiscalNote.AuthorizedAt = DateTime.UtcNow;
                fiscalNote.AuthorizationProtocol = $"PROT-{DateTime.UtcNow:yyyyMMddHHmmss}";
                fiscalNote.XmlContent = GenerateMockXmlForService(fiscalNote, client, coworking, config);
                fiscalNote.ViewUrl = $"{baseUrl}/nfe/view/{fiscalNote.AccessKey}";
                fiscalNote.PdfUrl = $"{baseUrl}/nfe/pdf/{fiscalNote.AccessKey}";
                fiscalNote.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation($"NF de serviço emitida com sucesso (simulado): {fiscalNote.AccessKey}");

                // Log de auditoria
                await LogAuditAsync(
                    GetCurrentUserId(),
                    "NF de serviço emitida (SEFAZ)",
                    "FiscalNote",
                    fiscalNote.Id,
                    null,
                    new { 
                        AccessKey = fiscalNote.AccessKey,
                        Number = fiscalNote.Number,
                        Status = fiscalNote.Status,
                        Amount = fiscalNote.Amount,
                        ServiceAmount = fiscalNote.ServiceAmount,
                        State = config.State,
                        Environment = config.Environment,
                        ReservationId = fiscalNote.ReservationId,
                        ContractId = fiscalNote.ContractId,
                        CoworkingId = fiscalNote.CoworkingId,
                        IntegrationMode = "Simulado"
                    }
                );

                return fiscalNote;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao emitir NF de serviço: {ex.Message}");
                fiscalNote.Status = FiscalNoteStatus.Erro;
                fiscalNote.ErrorMessage = ex.Message;
                fiscalNote.UpdatedAt = DateTime.UtcNow;
                throw;
            }
        }

        public async Task<FiscalNote> GetFiscalNoteStatusAsync(string accessKey)
        {
            var fiscalNote = await _context.FiscalNotes
                .FirstOrDefaultAsync(fn => fn.AccessKey == accessKey);

            if (fiscalNote == null)
            {
                throw new InvalidOperationException("Nota fiscal não encontrada.");
            }

            // TODO: Consultar status real na SEFAZ ES
            return fiscalNote;
        }

        // Métodos auxiliares (substituir por implementação real)
        private string GenerateFiscalNoteNumber()
        {
            // Formato: 9 dígitos sequenciais
            var random = new Random();
            return random.Next(1, 999999999).ToString("D9");
        }

        private string GenerateAccessKey()
        {
            // Chave de acesso NFe: 44 dígitos
            // Formato: UF (2) + AAMM (4) + CNPJ (14) + Modelo (2) + Série (3) + Número (9) + Tipo Emissão (1) + Código Numérico (8) + DV (1)
            var random = new Random();
            var uf = "32"; // ES
            var aamm = DateTime.UtcNow.ToString("yyMM");
            var cnpj = "00000000000000"; // Será substituído pelo CNPJ real
            var modelo = "55"; // NFe
            var serie = "001";
            var numero = GenerateFiscalNoteNumber();
            var tipoEmissao = "1";
            var codigoNumerico = random.Next(10000000, 99999999).ToString();
            var dv = random.Next(0, 9).ToString();

            return $"{uf}{aamm}{cnpj}{modelo}{serie}{numero}{tipoEmissao}{codigoNumerico}{dv}";
        }

        private string GenerateAccessKeyForPartner(string cnpj, string state)
        {
            // Chave de acesso NFe: 44 dígitos para parceiro
            // Formato: UF (2) + AAMM (4) + CNPJ (14) + Modelo (2) + Série (3) + Número (9) + Tipo Emissão (1) + Código Numérico (8) + DV (1)
            var random = new Random();
            var uf = GetStateCode(state);
            var aamm = DateTime.UtcNow.ToString("yyMM");
            var cnpjClean = cnpj.Replace(".", "").Replace("/", "").Replace("-", "").PadLeft(14, '0');
            var modelo = "55"; // NFe
            var serie = "001";
            var numero = GenerateFiscalNoteNumber();
            var tipoEmissao = "1";
            var codigoNumerico = random.Next(10000000, 99999999).ToString();
            var dv = random.Next(0, 9).ToString();

            return $"{uf}{aamm}{cnpjClean}{modelo}{serie}{numero}{tipoEmissao}{codigoNumerico}{dv}";
        }

        private string GetStateCode(string state)
        {
            // Mapeamento de UF para código numérico
            var stateCodes = new Dictionary<string, string>
            {
                { "AC", "12" }, { "AL", "27" }, { "AP", "16" }, { "AM", "13" },
                { "BA", "29" }, { "CE", "23" }, { "DF", "53" }, { "ES", "32" },
                { "GO", "52" }, { "MA", "21" }, { "MT", "51" }, { "MS", "50" },
                { "MG", "31" }, { "PA", "15" }, { "PB", "25" }, { "PR", "41" },
                { "PE", "26" }, { "PI", "22" }, { "RJ", "33" }, { "RN", "24" },
                { "RS", "43" }, { "RO", "11" }, { "RR", "14" }, { "SC", "42" },
                { "SP", "35" }, { "SE", "28" }, { "TO", "17" }
            };

            return stateCodes.TryGetValue(state.ToUpper(), out var code) ? code : "32"; // Default ES
        }

        private string GenerateVerificationCode()
        {
            // Código de verificação: 8 dígitos
            var random = new Random();
            return random.Next(10000000, 99999999).ToString();
        }

        private string GenerateMockXml(FiscalNote fiscalNote, Client client, Coworking coworking, SefazConfiguration config)
        {
            // XML mockado - substituir por geração real conforme layout da SEFAZ ES
            // NF de intermediação (comissão) - emitida pela plataforma
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<NFe>
    <infNFe>
        <ide>
            <cUF>{GetStateCode(config.State)}</cUF>
            <natOp>Intermediação de serviços</natOp>
            <mod>55</mod>
            <serie>001</serie>
            <nNF>{fiscalNote.Number}</nNF>
            <dhEmi>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}</dhEmi>
            <tpNF>1</tpNF>
            <idDest>1</idDest>
            <cMunFG>3205309</cMunFG>
            <tpImp>1</tpImp>
            <tpEmis>1</tpEmis>
            <cDV>1</cDV>
            <tpAmb>{(config.Environment == "Producao" ? "1" : "2")}</tpAmb>
        </ide>
        <emit>
            <CNPJ>{config.PlatformCNPJ.Replace(".", "").Replace("/", "").Replace("-", "")}</CNPJ>
            <xNome>Klyon Hub Platform</xNome>
            <IE>{config.PlatformMunicipalRegistration}</IE>
            <CRT>1</CRT>
        </emit>
        <dest>
            <CNPJ>{coworking.CNPJ.Replace(".", "").Replace("/", "").Replace("-", "")}</CNPJ>
            <xNome>{coworking.Name}</xNome>
        </dest>
        <det>
            <nItem>1</nItem>
            <prod>
                <cProd>INTERMEDIACAO</cProd>
                <xProd>Intermediação de serviços - Comissão</xProd>
                <NCM>00000000</NCM>
                <CFOP>5952</CFOP>
                <uCom>UN</uCom>
                <qCom>1.0000</qCom>
                <vUnCom>{(fiscalNote.CommissionAmount ?? 0m):F2}</vUnCom>
                <vProd>{(fiscalNote.CommissionAmount ?? 0m):F2}</vProd>
                <cEAN></cEAN>
                <cEANTrib></cEANTrib>
            </prod>
            <imposto>
                <vTotTrib>0.00</vTotTrib>
                <ISSQN>
                    <vBC>{(fiscalNote.CommissionAmount ?? 0m):F2}</vBC>
                    <vAliq>{config.ISSAliquot:F2}</vAliq>
                    <vISSQN>{Math.Round((fiscalNote.CommissionAmount ?? 0m) * (config.ISSAliquot / 100m), 2):F2}</vISSQN>
                    <cMunFG>3205309</cMunFG>
                    <cListServ>{config.IntermediationServiceCode}</cListServ>
                </ISSQN>
            </imposto>
        </det>
        <total>
            <ICMSTot>
                <vNF>{(fiscalNote.CommissionAmount ?? 0m):F2}</vNF>
            </ICMSTot>
        </total>
    </infNFe>
</NFe>";
        }

        private string GenerateMockXmlForService(FiscalNote fiscalNote, Client client, Coworking coworking, CoworkingSefazConfiguration config)
        {
            // XML mockado - substituir por geração real conforme layout da SEFAZ ES
            // NF de serviço - emitida pelo parceiro
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<NFe>
    <infNFe>
        <ide>
            <cUF>{GetStateCode(config.State)}</cUF>
            <natOp>Prestação de serviços</natOp>
            <mod>55</mod>
            <serie>001</serie>
            <nNF>{fiscalNote.Number}</nNF>
            <dhEmi>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}</dhEmi>
            <tpNF>1</tpNF>
            <idDest>1</idDest>
            <cMunFG>3205309</cMunFG>
            <tpImp>1</tpImp>
            <tpEmis>1</tpEmis>
            <cDV>1</cDV>
            <tpAmb>{(config.Environment == "Producao" ? "1" : "2")}</tpAmb>
        </ide>
        <emit>
            <CNPJ>{config.PartnerCNPJ.Replace(".", "").Replace("/", "").Replace("-", "")}</CNPJ>
            <xNome>{coworking.Name}</xNome>
            <IE>{config.PartnerMunicipalRegistration}</IE>
            <CRT>1</CRT>
        </emit>
        <dest>
            <CNPJ>{client.CNPJ?.Replace(".", "").Replace("/", "").Replace("-", "") ?? client.CPF?.Replace(".", "").Replace("-", "")}</CNPJ>
            <xNome>{client.RazaoSocial ?? client.NomeFantasia ?? client.User?.FullName}</xNome>
        </dest>
        <det>
            <nItem>1</nItem>
            <prod>
                <cProd>SERVICO</cProd>
                <xProd>{config.ServiceDescription}</xProd>
                <NCM>00000000</NCM>
                <CFOP>5952</CFOP>
                <uCom>UN</uCom>
                <qCom>1.0000</qCom>
                <vUnCom>{(fiscalNote.ServiceAmount ?? fiscalNote.Amount):F2}</vUnCom>
                <vProd>{(fiscalNote.ServiceAmount ?? fiscalNote.Amount):F2}</vProd>
                <cEAN></cEAN>
                <cEANTrib></cEANTrib>
            </prod>
            <imposto>
                <vTotTrib>0.00</vTotTrib>
                <ISSQN>
                    <vBC>{(fiscalNote.ServiceAmount ?? fiscalNote.Amount):F2}</vBC>
                    <vAliq>{config.ISSAliquot:F2}</vAliq>
                    <vISSQN>{Math.Round((fiscalNote.ServiceAmount ?? fiscalNote.Amount) * (config.ISSAliquot / 100m), 2):F2}</vISSQN>
                    <cMunFG>3205309</cMunFG>
                    <cListServ>{config.ServiceCode}</cListServ>
                </ISSQN>
            </imposto>
        </det>
        <total>
            <ICMSTot>
                <vNF>{(fiscalNote.ServiceAmount ?? fiscalNote.Amount):F2}</vNF>
            </ICMSTot>
        </total>
    </infNFe>
</NFe>";
        }
    }
}
