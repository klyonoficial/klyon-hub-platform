using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    /// <summary>
    /// Serviço de integração com SEFAZ para emissão de notas fiscais
    /// Suporta múltiplos estados, cada um com sua própria configuração
    /// </summary>
    public class SefazService : ISefazService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SefazService> _logger;

        // Estados com integração implementada
        private static readonly HashSet<string> SupportedStates = new HashSet<string> { "ES" };

        public SefazService(ApplicationDbContext context, ILogger<SefazService> logger)
        {
            _context = context;
            _logger = logger;
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
        /// Obtém a configuração SEFAZ para um estado específico
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

        public async Task<FiscalNote> IssueIntermediationNoteAsync(FiscalNote fiscalNote, SefazConfiguration config)
        {
            try
            {
                // Verificar se o estado tem integração implementada
                if (!IsStateSupported(config.State))
                {
                    throw new NotSupportedException($"Integração SEFAZ ainda não implementada para o estado {config.State}. Estados suportados: {string.Join(", ", SupportedStates)}");
                }

                // TODO: Implementar integração real com SEFAZ do estado específico
                // Por enquanto, simula a emissão
                
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

                // Determinar URL baseada no ambiente
                var baseUrl = config.Environment == "Producao" 
                    ? config.ProductionUrl 
                    : config.HomologationUrl;

                if (string.IsNullOrEmpty(baseUrl))
                {
                    throw new InvalidOperationException($"URL da SEFAZ não configurada para ambiente {config.Environment}.");
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

                // Preparar dados para emissão da NF
                // NOTA: Esta é uma implementação básica. A integração real com SEFAZ ES requer:
                // 1. Certificado digital A1 ou A3
                // 2. Biblioteca específica da SEFAZ ES (ex: NFe.ES)
                // 3. Geração do XML da NF-e conforme layout da SEFAZ
                // 4. Assinatura digital do XML
                // 5. Envio via webservice da SEFAZ
                // 6. Processamento da resposta e atualização do registro

                // Simulação de emissão (substituir por integração real)
                fiscalNote.Number = GenerateFiscalNoteNumber();
                fiscalNote.AccessKey = GenerateAccessKey();
                fiscalNote.VerificationCode = GenerateVerificationCode();
                fiscalNote.Status = "Emitida";
                fiscalNote.AuthorizedAt = DateTime.UtcNow;
                fiscalNote.AuthorizationProtocol = $"PROT-{DateTime.UtcNow:yyyyMMddHHmmss}";
                fiscalNote.XmlContent = GenerateMockXml(fiscalNote, client, coworking, config);
                fiscalNote.ViewUrl = $"{baseUrl}/nfe/view/{fiscalNote.AccessKey}";
                fiscalNote.PdfUrl = $"{baseUrl}/nfe/pdf/{fiscalNote.AccessKey}";
                fiscalNote.UpdatedAt = DateTime.UtcNow;

                _logger.LogInformation($"NF de intermediação emitida com sucesso: {fiscalNote.AccessKey}");

                return fiscalNote;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao emitir NF de intermediação: {ex.Message}");
                fiscalNote.Status = "Erro";
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

                if (fiscalNote.Status != "Emitida")
                {
                    throw new InvalidOperationException("Apenas notas fiscais emitidas podem ser canceladas.");
                }

                // TODO: Implementar cancelamento real na SEFAZ ES
                fiscalNote.Status = "Cancelada";
                fiscalNote.CancelledAt = DateTime.UtcNow;
                fiscalNote.CancellationReason = cancellationReason;
                fiscalNote.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Erro ao cancelar NF: {ex.Message}");
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

        private string GenerateVerificationCode()
        {
            // Código de verificação: 8 dígitos
            var random = new Random();
            return random.Next(10000000, 99999999).ToString();
        }

        private string GenerateMockXml(FiscalNote fiscalNote, Client client, Coworking coworking, SefazConfiguration config)
        {
            // XML mockado - substituir por geração real conforme layout da SEFAZ ES
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<NFe>
    <infNFe>
        <ide>
            <cUF>32</cUF>
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
            <CNPJ>{config.PlatformCNPJ}</CNPJ>
            <xNome>Klyon Hub Platform</xNome>
            <IE>{config.PlatformMunicipalRegistration}</IE>
            <CRT>1</CRT>
        </emit>
        <dest>
            <CNPJ>{coworking.CNPJ}</CNPJ>
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
    }
}
