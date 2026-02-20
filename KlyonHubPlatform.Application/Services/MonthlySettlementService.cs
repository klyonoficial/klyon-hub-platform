using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Xml.Linq;
namespace KlyonHubPlatform.Application.Services
{
    public class MonthlySettlementService : IMonthlySettlementService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MonthlySettlementService> _logger;
        private readonly IConfiguration _configuration;
        private readonly StorageServiceFactory _storageServiceFactory;
        private readonly IPagBankService _pagBankService;
        private readonly IFocusNFeService? _focusNFeService;
        private readonly IAuditService? _auditService;
        private static readonly HashSet<(int Month, int Day)> FixedHolidays = new()
        {
            (1, 1),
            (4, 21),
            (5, 1),
            (9, 7),
            (10, 12),
            (11, 2),
            (11, 15),
            (12, 25)
        };
        public MonthlySettlementService(
            ApplicationDbContext context,
            ILogger<MonthlySettlementService> logger,
            IConfiguration configuration,
            StorageServiceFactory storageServiceFactory,
            IPagBankService pagBankService,
            IFocusNFeService? focusNFeService = null,
            IAuditService? auditService = null)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _storageServiceFactory = storageServiceFactory;
            _pagBankService = pagBankService;
            _focusNFeService = focusNFeService;
            _auditService = auditService;
        }
        #region Fechamento Mensal
        public async Task<MonthlySettlement> GetOrCreateOpenSettlementAsync(int coworkingId, int month, int year)
        {
            var settlement = await _context.MonthlySettlements
                .Include(s => s.SettlementCommissions)
                .FirstOrDefaultAsync(s => s.CoworkingId == coworkingId 
                    && s.ReferenceMonth == month 
                    && s.ReferenceYear == year);
            if (settlement == null)
            {
                settlement = new MonthlySettlement
                {
                    CoworkingId = coworkingId,
                    ReferenceMonth = month,
                    ReferenceYear = year,
                    Status = SettlementStatus.Open,
                    TotalAmount = 0,
                    TotalGrossAmount = 0,
                    TotalPlatformCommission = 0,
                    CommissionCount = 0,
                    CreatedAt = DateTime.UtcNow
                };
                _context.MonthlySettlements.Add(settlement);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Criado novo fechamento mensal. CoworkingId: {CoworkingId}, Período: {Month}/{Year}", 
                    coworkingId, month, year);
            }
            return settlement;
        }
        public async Task AddCommissionToSettlementAsync(int commissionId)
        {
            var commission = await _context.Commissions
                .Include(c => c.Payment)
                .FirstOrDefaultAsync(c => c.Id == commissionId);
            if (commission == null)
            {
                _logger.LogWarning("Comissão não encontrada: {CommissionId}", commissionId);
                return;
            }
            var existingLink = await _context.Set<SettlementCommission>()
                .AnyAsync(sc => sc.CommissionId == commissionId);
            if (existingLink)
            {
                _logger.LogWarning("Comissão já está em um fechamento: {CommissionId}", commissionId);
                return;
            }
            var paymentDate = commission.Payment?.CreatedAt ?? commission.CreatedAt;
            var month = paymentDate.Month;
            var year = paymentDate.Year;
            var settlement = await GetOrCreateOpenSettlementAsync(commission.CoworkingId, month, year);
            if (settlement.Status != SettlementStatus.Open)
            {
                _logger.LogWarning("Fechamento já foi consolidado. SettlementId: {SettlementId}", settlement.Id);
                return;
            }
            var partnerAmount = commission.TotalPaymentAmount - commission.Amount;
            var settlementCommission = new SettlementCommission
            {
                MonthlySettlementId = settlement.Id,
                CommissionId = commissionId,
                Amount = partnerAmount,
                CreatedAt = DateTime.UtcNow
            };
            _context.Set<SettlementCommission>().Add(settlementCommission);
            settlement.TotalAmount += partnerAmount;
            settlement.TotalGrossAmount += commission.TotalPaymentAmount;
            settlement.TotalPlatformCommission += commission.Amount;
            settlement.CommissionCount++;
            settlement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Comissão adicionada ao fechamento. CommissionId: {CommissionId}, SettlementId: {SettlementId}, Valor: {Amount}", 
                commissionId, settlement.Id, partnerAmount);
        }
        public async Task<MonthlySettlement> CloseMonthAsync(int coworkingId, int month, int year)
        {
            var settlement = await _context.MonthlySettlements
                .Include(s => s.Coworking)
                .FirstOrDefaultAsync(s => s.CoworkingId == coworkingId 
                    && s.ReferenceMonth == month 
                    && s.ReferenceYear == year);
            if (settlement == null)
            {
                throw new InvalidOperationException($"Fechamento não encontrado para {month}/{year}");
            }
            if (settlement.Status != SettlementStatus.Open)
            {
                return settlement;
            }
            if (settlement.TotalAmount <= 0)
            {
                _logger.LogInformation("Fechamento sem valor. Cancelando. SettlementId: {Id}", settlement.Id);
                settlement.Status = SettlementStatus.Cancelled;
                settlement.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return settlement;
            }
            var closingDate = DateTime.UtcNow;
            var nextMonth = new DateTime(year, month, 1).AddMonths(1);
            var transferDate = GetLastBusinessDayOfMonth(nextMonth.Year, nextMonth.Month);
            var invoiceDeadline = CalculateInvoiceDeadline(closingDate);
            settlement.PixKey = settlement.Coworking?.BankPixKey;
            settlement.Status = SettlementStatus.AwaitingInvoice;
            settlement.ClosedAt = closingDate;
            settlement.ScheduledTransferDate = transferDate;
            settlement.InvoiceDeadline = invoiceDeadline;
            settlement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Fechamento mensal consolidado. SettlementId: {Id}, Valor: {Amount}, Data transferência: {TransferDate}", 
                settlement.Id, settlement.TotalAmount, transferDate);
            return settlement;
        }
        public async Task CloseAllOpenMonthsAsync()
        {
            var now = DateTime.UtcNow;
            var currentMonth = now.Month;
            var currentYear = now.Year;
            var openSettlements = await _context.MonthlySettlements
                .Include(s => s.Coworking)
                .Where(s => s.Status == SettlementStatus.Open)
                .Where(s => s.ReferenceYear < currentYear || 
                           (s.ReferenceYear == currentYear && s.ReferenceMonth < currentMonth))
                .ToListAsync();
            foreach (var settlement in openSettlements)
            {
                try
                {
                    await CloseMonthAsync(settlement.CoworkingId, settlement.ReferenceMonth, settlement.ReferenceYear);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao fechar mês. SettlementId: {Id}", settlement.Id);
                }
            }
            _logger.LogInformation("Processados {Count} fechamentos mensais", openSettlements.Count);
        }
        public async Task<List<MonthlySettlement>> GetSettlementsByCoworkingAsync(int coworkingId)
        {
            return await _context.MonthlySettlements
                .Include(s => s.PartnerInvoices)
                .Where(s => s.CoworkingId == coworkingId)
                .OrderByDescending(s => s.ReferenceYear)
                .ThenByDescending(s => s.ReferenceMonth)
                .ToListAsync();
        }
        public async Task<List<MonthlySettlement>> GetSettlementsAwaitingInvoiceAsync()
        {
            return await _context.MonthlySettlements
                .Include(s => s.Coworking)
                .Where(s => s.Status == SettlementStatus.AwaitingInvoice)
                .OrderBy(s => s.InvoiceDeadline)
                .ToListAsync();
        }
        public async Task<List<MonthlySettlement>> GetSettlementsWithInvoiceToValidateAsync()
        {
            return await _context.MonthlySettlements
                .Include(s => s.Coworking)
                .Include(s => s.PartnerInvoices)
                .Where(s => s.Status == SettlementStatus.InvoiceReceived)
                .OrderBy(s => s.ClosedAt)
                .ToListAsync();
        }
        public async Task<List<MonthlySettlement>> GetSettlementsReadyForTransferAsync()
        {
            var today = DateTime.UtcNow.Date;
            return await _context.MonthlySettlements
                .Include(s => s.Coworking)
                .Where(s => s.Status == SettlementStatus.InvoiceApproved || s.Status == SettlementStatus.TransferScheduled)
                .Where(s => s.ScheduledTransferDate <= today)
                .OrderBy(s => s.ScheduledTransferDate)
                .ToListAsync();
        }
        public async Task<MonthlySettlement?> GetSettlementByIdAsync(int settlementId)
        {
            return await _context.MonthlySettlements
                .Include(s => s.Coworking)
                .Include(s => s.PartnerInvoices)
                .Include(s => s.SettlementCommissions)
                    .ThenInclude(sc => sc.Commission)
                        .ThenInclude(c => c.Payment)
                .FirstOrDefaultAsync(s => s.Id == settlementId);
        }
        #endregion
        #region Nota Fiscal do Parceiro
        public async Task<PartnerInvoice> UploadPartnerInvoiceAsync(int settlementId, int coworkingId, 
            Stream pdfStream, string pdfFileName, Stream? xmlStream, string? xmlFileName, 
            string? invoiceNumber, decimal? invoiceAmount, string? partnerNotes)
        {
            var settlement = await _context.MonthlySettlements
                .FirstOrDefaultAsync(s => s.Id == settlementId && s.CoworkingId == coworkingId);
            if (settlement == null)
            {
                throw new InvalidOperationException("Fechamento não encontrado");
            }
            if (settlement.Status != SettlementStatus.AwaitingInvoice && settlement.Status != SettlementStatus.InvoiceRejected)
            {
                throw new InvalidOperationException("Este fechamento não está aguardando nota fiscal");
            }
            var storageService = await _storageServiceFactory.GetStorageServiceAsync();
            using var pdfMemoryStream = new MemoryStream();
            await pdfStream.CopyToAsync(pdfMemoryStream);
            var pdfPath = await storageService.SaveFileAsync(pdfMemoryStream.ToArray(), pdfFileName, 
                $"partner-invoices/{coworkingId}/{settlement.ReferenceYear}/{settlement.ReferenceMonth}");
            string? xmlPath = null;
            string? xmlContent = null;
            if (xmlStream != null && !string.IsNullOrEmpty(xmlFileName))
            {
                using var xmlMemoryStream = new MemoryStream();
                await xmlStream.CopyToAsync(xmlMemoryStream);
                xmlPath = await storageService.SaveFileAsync(xmlMemoryStream.ToArray(), xmlFileName, 
                    $"partner-invoices/{coworkingId}/{settlement.ReferenceYear}/{settlement.ReferenceMonth}");
                xmlStream.Position = 0;
                using var reader = new StreamReader(xmlStream);
                xmlContent = await reader.ReadToEndAsync();
            }
            var invoice = new PartnerInvoice
            {
                MonthlySettlementId = settlementId,
                CoworkingId = coworkingId,
                Type = PartnerInvoiceType.ServiceInvoice,
                Status = InvoiceValidationStatus.Submitted,
                InvoiceAmount = invoiceAmount ?? 0,
                ExpectedAmount = settlement.TotalAmount,
                InvoiceNumber = invoiceNumber,
                PdfFilePath = pdfPath,
                XmlFilePath = xmlPath,
                XmlContent = xmlContent,
                SubmittedAt = DateTime.UtcNow,
                Deadline = settlement.InvoiceDeadline,
                PartnerNotes = partnerNotes,
                CreatedAt = DateTime.UtcNow
            };
            _context.Set<PartnerInvoice>().Add(invoice);
            settlement.Status = SettlementStatus.InvoiceReceived;
            settlement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await ValidateInvoiceAutomaticallyAsync(invoice.Id);
            _logger.LogInformation("NF do parceiro recebida. InvoiceId: {Id}, SettlementId: {SettlementId}", 
                invoice.Id, settlementId);
            return invoice;
        }
        public async Task<(bool IsValid, string Message)> ValidateInvoiceAutomaticallyAsync(int invoiceId)
        {
            var invoice = await _context.Set<PartnerInvoice>()
                .Include(i => i.MonthlySettlement)
                    .ThenInclude(s => s.Coworking)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null)
            {
                return (false, "NF não encontrada");
            }
            var validationMessages = new List<string>();
            var isValid = true;
            var tolerance = invoice.ExpectedAmount * 0.01m;
            if (Math.Abs(invoice.InvoiceAmount - invoice.ExpectedAmount) > tolerance)
            {
                validationMessages.Add($"Valor divergente. Esperado: R$ {invoice.ExpectedAmount:N2}, Informado: R$ {invoice.InvoiceAmount:N2}");
                isValid = false;
            }
            if (!string.IsNullOrEmpty(invoice.XmlContent))
            {
                try
                {
                    var xmlValidation = ValidateXmlContent(invoice.XmlContent, invoice.MonthlySettlement.Coworking);
                    if (!xmlValidation.IsValid)
                    {
                        validationMessages.AddRange(xmlValidation.Messages);
                        isValid = false;
                    }
                    else
                    {
                        invoice.IssuerCnpj = xmlValidation.IssuerCnpj;
                        invoice.IssuerName = xmlValidation.IssuerName;
                        invoice.IssueDate = xmlValidation.IssueDate;
                        if (xmlValidation.InvoiceAmount > 0)
                        {
                            invoice.InvoiceAmount = xmlValidation.InvoiceAmount;
                        }
                    }
                }
                catch (Exception ex)
                {
                    validationMessages.Add($"Erro ao processar XML: {ex.Message}");
                    isValid = false;
                }
            }
            invoice.AutoValidationPassed = isValid;
            invoice.AutoValidationMessage = string.Join("; ", validationMessages);
            invoice.Status = isValid ? InvoiceValidationStatus.Submitted : InvoiceValidationStatus.Submitted;
            invoice.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            var message = isValid 
                ? "Validação automática OK. Aguardando aprovação do administrador."
                : $"Validação automática encontrou problemas: {invoice.AutoValidationMessage}";
            return (isValid, message);
        }
        private (bool IsValid, List<string> Messages, string? IssuerCnpj, string? IssuerName, DateTime? IssueDate, decimal InvoiceAmount) 
            ValidateXmlContent(string xmlContent, Coworking coworking)
        {
            var messages = new List<string>();
            string? issuerCnpj = null;
            string? issuerName = null;
            DateTime? issueDate = null;
            decimal invoiceAmount = 0;
            try
            {
                var xml = XDocument.Parse(xmlContent);
                var ns = xml.Root?.GetDefaultNamespace() ?? XNamespace.None;
                var prestador = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "Prestador");
                if (prestador != null)
                {
                    issuerCnpj = prestador.Descendants().FirstOrDefault(e => e.Name.LocalName == "Cnpj")?.Value;
                    issuerName = prestador.Descendants().FirstOrDefault(e => e.Name.LocalName == "RazaoSocial")?.Value;
                }
                var valorServicos = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "ValorServicos" || e.Name.LocalName == "ValorLiquidoNfse");
                if (valorServicos != null && decimal.TryParse(valorServicos.Value.Replace(",", "."), 
                    System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var valor))
                {
                    invoiceAmount = valor;
                }
                var dataEmissao = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "DataEmissao" || e.Name.LocalName == "dhEmi");
                if (dataEmissao != null && DateTime.TryParse(dataEmissao.Value, out var data))
                {
                    issueDate = data;
                }
                if (!string.IsNullOrEmpty(issuerCnpj))
                {
                    var expectedCnpj = coworking.CNPJ?.Replace(".", "").Replace("/", "").Replace("-", "");
                    var xmlCnpj = issuerCnpj.Replace(".", "").Replace("/", "").Replace("-", "");
                    if (expectedCnpj != xmlCnpj)
                    {
                        messages.Add($"CNPJ do emitente divergente. Esperado: {coworking.CNPJ}, XML: {issuerCnpj}");
                    }
                }
            }
            catch (Exception ex)
            {
                messages.Add($"Erro ao analisar XML: {ex.Message}");
            }
            return (messages.Count == 0, messages, issuerCnpj, issuerName, issueDate, invoiceAmount);
        }
        public async Task<PartnerInvoice> ApproveInvoiceAsync(int invoiceId, int adminUserId, string? notes)
        {
            var invoice = await _context.Set<PartnerInvoice>()
                .Include(i => i.MonthlySettlement)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null)
            {
                throw new InvalidOperationException("NF não encontrada");
            }
            invoice.Status = InvoiceValidationStatus.Approved;
            invoice.ValidatedBy = adminUserId;
            invoice.ValidatedAt = DateTime.UtcNow;
            invoice.AdminNotes = notes;
            invoice.UpdatedAt = DateTime.UtcNow;
            invoice.MonthlySettlement.Status = SettlementStatus.InvoiceApproved;
            invoice.MonthlySettlement.ApprovedBy = adminUserId;
            invoice.MonthlySettlement.ApprovedAt = DateTime.UtcNow;
            invoice.MonthlySettlement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("NF aprovada. InvoiceId: {Id}, AdminId: {AdminId}", invoiceId, adminUserId);
            return invoice;
        }
        public async Task<PartnerInvoice> RejectInvoiceAsync(int invoiceId, int adminUserId, string rejectionReason)
        {
            var invoice = await _context.Set<PartnerInvoice>()
                .Include(i => i.MonthlySettlement)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null)
            {
                throw new InvalidOperationException("NF não encontrada");
            }
            invoice.Status = InvoiceValidationStatus.Rejected;
            invoice.ValidatedBy = adminUserId;
            invoice.ValidatedAt = DateTime.UtcNow;
            invoice.RejectionReason = rejectionReason;
            invoice.UpdatedAt = DateTime.UtcNow;
            invoice.MonthlySettlement.Status = SettlementStatus.InvoiceRejected;
            invoice.MonthlySettlement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("NF rejeitada. InvoiceId: {Id}, Motivo: {Reason}", invoiceId, rejectionReason);
            return invoice;
        }
        public async Task<PartnerInvoice> GenerateAutomaticInvoiceAsync(int settlementId)
        {
            var settlement = await _context.MonthlySettlements
                .Include(s => s.Coworking)
                .FirstOrDefaultAsync(s => s.Id == settlementId);
            if (settlement == null)
            {
                throw new InvalidOperationException("Fechamento não encontrado");
            }
            _logger.LogInformation("Gerando NF automática para fechamento {Id}", settlementId);
            var invoice = new PartnerInvoice
            {
                MonthlySettlementId = settlementId,
                CoworkingId = settlement.CoworkingId,
                Type = PartnerInvoiceType.AutoGenerated,
                Status = InvoiceValidationStatus.AutoGenerated,
                InvoiceAmount = settlement.TotalAmount,
                ExpectedAmount = settlement.TotalAmount,
                IsAutoGenerated = true,
                SubmittedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            _context.Set<PartnerInvoice>().Add(invoice);
            settlement.Status = SettlementStatus.InvoiceReceived;
            settlement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return invoice;
        }
        public async Task ProcessExpiredInvoiceDeadlinesAsync()
        {
            var now = DateTime.UtcNow;
            var expiredSettlements = await _context.MonthlySettlements
                .Where(s => s.Status == SettlementStatus.AwaitingInvoice)
                .Where(s => s.InvoiceDeadline < now)
                .ToListAsync();
            foreach (var settlement in expiredSettlements)
            {
                try
                {
                    await GenerateAutomaticInvoiceAsync(settlement.Id);
                    _logger.LogInformation("NF automática gerada para fechamento expirado. Id: {Id}", settlement.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao gerar NF automática. SettlementId: {Id}", settlement.Id);
                }
            }
        }
        public async Task<PartnerInvoice?> GetInvoiceBySettlementAsync(int settlementId)
        {
            return await _context.Set<PartnerInvoice>()
                .Where(i => i.MonthlySettlementId == settlementId)
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefaultAsync();
        }
        #endregion
        #region Transferência
        public async Task ScheduleTransferAsync(int settlementId)
        {
            var settlement = await _context.MonthlySettlements.FindAsync(settlementId);
            if (settlement == null) return;
            if (settlement.Status != SettlementStatus.InvoiceApproved)
            {
                return;
            }
            settlement.Status = SettlementStatus.TransferScheduled;
            settlement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        public async Task ProcessScheduledTransfersAsync()
        {
            var today = DateTime.UtcNow.Date;
            var readySettlements = await GetSettlementsReadyForTransferAsync();
            foreach (var settlement in readySettlements)
            {
                try
                {
                    await ExecuteTransferAsync(settlement.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao executar transferência. SettlementId: {Id}", settlement.Id);
                }
            }
        }
        public async Task<MonthlySettlement> ExecuteTransferAsync(int settlementId)
        {
            var settlement = await _context.MonthlySettlements
                .Include(s => s.Coworking)
                .FirstOrDefaultAsync(s => s.Id == settlementId);
            if (settlement == null)
            {
                throw new InvalidOperationException("Fechamento não encontrado");
            }
            if (string.IsNullOrEmpty(settlement.PixKey) && string.IsNullOrEmpty(settlement.Coworking?.BankPixKey))
            {
                settlement.Status = SettlementStatus.TransferFailed;
                settlement.FailureReason = "Chave PIX não cadastrada";
                settlement.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                throw new InvalidOperationException("Chave PIX do parceiro não cadastrada");
            }
            var pixKey = settlement.PixKey ?? settlement.Coworking?.BankPixKey;
            settlement.Status = SettlementStatus.TransferProcessing;
            settlement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            try
            {
                _logger.LogInformation("Executando transferência PIX. SettlementId: {Id}, Valor: {Amount}, PIX: {PixKey}", 
                    settlementId, settlement.TotalAmount, pixKey);
                var pixKeyType = DeterminePixKeyType(pixKey!);
                var referenceId = $"SETTLE-{settlementId}-{DateTime.UtcNow:yyyyMMdd}";
                var description = $"Repasse {settlement.ReferenceMonth:D2}/{settlement.ReferenceYear}";
                var transferResult = await _pagBankService.CreatePixTransferAsync(
                    referenceId,
                    settlement.TotalAmount,
                    pixKey!,
                    pixKeyType,
                    settlement.Coworking?.Name ?? "Parceiro",
                    description
                );
                if (!transferResult.Success)
                {
                    throw new InvalidOperationException($"Falha na transferência PIX: {transferResult.ErrorMessage}");
                }
                settlement.ExternalTransferId = transferResult.TransferId;
                settlement.Status = SettlementStatus.Completed;
                settlement.TransferredAt = DateTime.UtcNow;
                settlement.UpdatedAt = DateTime.UtcNow;
                var commissionIds = await _context.Set<SettlementCommission>()
                    .Where(sc => sc.MonthlySettlementId == settlementId)
                    .Select(sc => sc.CommissionId)
                    .ToListAsync();
                foreach (var commissionId in commissionIds)
                {
                    var commission = await _context.Commissions.FindAsync(commissionId);
                    if (commission != null)
                    {
                        commission.IsTransferred = true;
                        commission.TransferredAt = DateTime.UtcNow;
                    }
                }
                await _context.SaveChangesAsync();
                _logger.LogInformation("Transferência concluída. SettlementId: {Id}, TransferId: {TransferId}", 
                    settlementId, transferResult.TransferId);
            }
            catch (Exception ex)
            {
                settlement.Status = SettlementStatus.TransferFailed;
                settlement.FailureReason = ex.Message;
                settlement.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                throw;
            }
            return settlement;
        }
        private string DeterminePixKeyType(string pixKey)
        {
            var cleanKey = pixKey.Replace(".", "").Replace("-", "").Replace("/", "");
            if (cleanKey.Length == 11 && cleanKey.All(char.IsDigit))
                return "cpf";
            if (cleanKey.Length == 14 && cleanKey.All(char.IsDigit))
                return "cnpj";
            if (pixKey.Contains("@"))
                return "email";
            if (pixKey.StartsWith("+55") || (cleanKey.Length >= 10 && cleanKey.Length <= 11 && cleanKey.All(char.IsDigit)))
                return "phone";
            if (cleanKey.Length == 32 || Guid.TryParse(pixKey, out _))
                return "evp";
            return "evp";
        }
        public async Task ProcessTransferCallbackAsync(string externalTransferId, bool success, string? failureReason)
        {
            var settlement = await _context.MonthlySettlements
                .FirstOrDefaultAsync(s => s.ExternalTransferId == externalTransferId);
            if (settlement == null)
            {
                _logger.LogWarning("Fechamento não encontrado para transferência: {ExternalId}", externalTransferId);
                return;
            }
            if (success)
            {
                settlement.Status = SettlementStatus.Completed;
                settlement.TransferredAt = DateTime.UtcNow;
            }
            else
            {
                settlement.Status = SettlementStatus.TransferFailed;
                settlement.FailureReason = failureReason;
            }
            settlement.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        #endregion
        #region Utilitários
        public DateTime GetLastBusinessDayOfMonth(int year, int month)
        {
            var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            while (!IsBusinessDay(lastDay))
            {
                lastDay = lastDay.AddDays(-1);
            }
            return lastDay;
        }
        public DateTime CalculateInvoiceDeadline(DateTime closingDate)
        {
            var deadline = closingDate;
            var businessDays = 0;
            while (businessDays < 5)
            {
                deadline = deadline.AddDays(1);
                if (IsBusinessDay(deadline))
                {
                    businessDays++;
                }
            }
            return deadline;
        }
        private bool IsBusinessDay(DateTime date)
        {
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                return false;
            }
            if (FixedHolidays.Contains((date.Month, date.Day)))
            {
                return false;
            }
            return true;
        }
        public async Task<SettlementDashboardSummary> GetDashboardSummaryAsync(int? coworkingId = null)
        {
            var query = _context.MonthlySettlements.AsQueryable();
            if (coworkingId.HasValue)
            {
                query = query.Where(s => s.CoworkingId == coworkingId.Value);
            }
            var settlements = await query.ToListAsync();
            return new SettlementDashboardSummary
            {
                TotalOpen = settlements.Count(s => s.Status == SettlementStatus.Open),
                TotalAwaitingInvoice = settlements.Count(s => s.Status == SettlementStatus.AwaitingInvoice),
                TotalInvoiceReceived = settlements.Count(s => s.Status == SettlementStatus.InvoiceReceived),
                TotalInvoiceApproved = settlements.Count(s => s.Status == SettlementStatus.InvoiceApproved || s.Status == SettlementStatus.TransferScheduled),
                TotalScheduled = settlements.Count(s => s.Status == SettlementStatus.TransferScheduled),
                TotalCompleted = settlements.Count(s => s.Status == SettlementStatus.Completed),
                TotalFailed = settlements.Count(s => s.Status == SettlementStatus.TransferFailed),
                TotalAmountPending = settlements.Where(s => s.Status != SettlementStatus.Completed && s.Status != SettlementStatus.Cancelled).Sum(s => s.TotalAmount),
                TotalAmountTransferred = settlements.Where(s => s.Status == SettlementStatus.Completed).Sum(s => s.TotalAmount)
            };
        }
        #endregion
    }
}