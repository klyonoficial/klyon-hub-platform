using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Application.Helpers;
using iTextSharp.text;
using iTextSharp.text.pdf;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.Extensions.Hosting;
using PdfDocument = iTextSharp.text.Document;
using DocumentModel = KlyonHubPlatform.Domain.Entities.Document;
using KlyonHubPlatform.Domain.Interfaces;
using KlyonHubPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using static KlyonHubPlatform.Domain.Core.Constants.ContractSigningConfigKeys;
namespace KlyonHubPlatform.Application.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IContractRepository _contractRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly ApplicationDbContext _context;
        private readonly IHostEnvironment _environment;
        private readonly StorageServiceFactory _storageFactory;
        private readonly ISystemConfigurationService _systemConfigService;
        private readonly ID4SignService _d4SignService;
        private readonly IClicksignService _clicksignService;
        private readonly IContractHistoryService _contractHistoryService;
        public DocumentService(
            IDocumentRepository documentRepository,
            IContractRepository contractRepository,
            IPaymentRepository paymentRepository,
            ApplicationDbContext context,
            IHostEnvironment environment,
            StorageServiceFactory storageFactory,
            ISystemConfigurationService systemConfigService,
            ID4SignService d4SignService,
            IClicksignService clicksignService,
            IContractHistoryService contractHistoryService)
        {
            _documentRepository = documentRepository;
            _contractRepository = contractRepository;
            _paymentRepository = paymentRepository;
            _context = context;
            _environment = environment;
            _storageFactory = storageFactory;
            _systemConfigService = systemConfigService;
            _d4SignService = d4SignService;
            _clicksignService = clicksignService;
            _contractHistoryService = contractHistoryService;
        }
        public async Task<bool> CanGenerateContractDocumentAsync(int contractId)
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return false;

            var hasPaidPayment = contract.Subscription != null
                && await _context.Payments.AnyAsync(
                    p => p.SubscriptionId == contract.Subscription!.Id
                         && p.Status == PaymentStatus.Paid);

            return AddressContractRules.CanGenerate(
                contract,
                contract.Documents ?? Enumerable.Empty<DocumentModel>(),
                hasPaidPayment);
        }

        public async Task<bool> CanRegenerateContractDocumentAsync(int contractId)
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                return false;

            var hasPaidPayment = contract.Subscription != null
                && await _context.Payments.AnyAsync(
                    p => p.SubscriptionId == contract.Subscription!.Id
                         && p.Status == PaymentStatus.Paid);

            return AddressContractRules.CanRegenerate(
                contract,
                contract.Documents ?? Enumerable.Empty<DocumentModel>(),
                hasPaidPayment);
        }

        public async Task<bool> CanGenerateFiscalContractDocumentAsync(int contractId) =>
            await CanGenerateContractDocumentAsync(contractId);

        public async Task EnsureContractDocumentAsync(int contractId)
        {
            if (!await CanGenerateContractDocumentAsync(contractId))
                return;

            await GenerateContractDocumentAsync(contractId);
        }

        public async Task EnsureFiscalContractDocumentAsync(int contractId) =>
            await EnsureContractDocumentAsync(contractId);

        public async Task<DocumentModel> GenerateContractDocumentAsync(int contractId, bool regenerate = false)
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");

            var party = ClientContractPartyRules.Evaluate(contract.Client);
            if (!party.IsComplete)
                throw new InvalidOperationException(party.ErrorMessage ?? ClientContractPartyRules.IncompleteMessage);

            var hasPaidPayment = contract.Subscription != null
                && await _context.Payments.AnyAsync(
                    p => p.SubscriptionId == contract.Subscription!.Id
                         && p.Status == PaymentStatus.Paid);

            var documents = contract.Documents ?? Enumerable.Empty<DocumentModel>();
            if (regenerate)
            {
                if (!AddressContractRules.CanRegenerate(contract, documents, hasPaidPayment))
                {
                    throw new InvalidOperationException(
                        "Não é possível regenerar contrato: o contrato precisa estar vigente, com pagamento confirmado e com todos os documentos de contrato do tipo atual vencidos.");
                }
            }
            else if (!AddressContractRules.CanGenerate(contract, documents, hasPaidPayment))
            {
                throw new InvalidOperationException(
                    "Não é possível gerar contrato: o contrato precisa estar vigente, com pagamento confirmado e sem documento de contrato para o tipo de serviço atual.");
            }

            var serviceKind = AddressContractRules.ResolveServiceKind(contract.Service?.Code)
                ?? throw new InvalidOperationException("Tipo de serviço do contrato não suporta geração de contrato.");

            var variant = FiscalAddressContractRules.ResolveVariant(contract.Client);
            var existingDocuments = await _context.Documents
                .Where(d => d.ContractId == contractId && d.Type == DocumentType.Contract)
                .ToListAsync();
            var existingMatch = existingDocuments.FirstOrDefault(d =>
                AddressContractRules.DocumentMatches(d.Description, serviceKind, variant));
            if (!regenerate && existingMatch != null)
                return existingMatch;

            var fileName = $"contract_{contract.ContractNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            byte[] pdfBytes;
            string description;
            var watermarkBytes = PdfWatermarkLoader.LoadKlyonLogo(_environment);

            if (serviceKind == AddressContractServiceKind.Fiscal)
            {
                var model = BuildFiscalAddressContractPdfModel(contract, variant);
                pdfBytes = FiscalAddressContractPdfBuilder.BuildPdf(model, watermarkBytes);
                description = FiscalAddressContractRules.DescriptionForVariant(variant);
            }
            else
            {
                var model = BuildFiscalAddressContractPdfModel(contract, variant);
                pdfBytes = CommercialAddressContractPdfBuilder.BuildPdf(model, watermarkBytes);
                description = CommercialAddressContractRules.DescriptionForVariant(variant);
            }

            var storageService = await _storageFactory.GetStorageServiceAsync();
            var provider = await _systemConfigService.GetValueAsync(StorageConfigKeys.Provider) ?? FtpDefaults.ProviderName;
            var (folderKey, defaultFolder) = GetContractsFolderConfigByProvider(provider);
            var folder = await _systemConfigService.GetValueAsync(folderKey) ?? defaultFolder;
            var filePath = await storageService.SaveFileAsync(pdfBytes, fileName, folder);
            var signerEmail = contract.Client.ContactName ?? contract.Client.RazaoSocial ?? contract.Client.NomeFantasia;
            var contractKindLabel = serviceKind == AddressContractServiceKind.Commercial ? "Comercial" : "Fiscal";
            var message = $"Contrato de Endereço {contractKindLabel} {contract.ContractNumber}. Por favor, assine o documento.";
            string? d4SignUuid = null;
            string? clicksignEnvelopeId = null;
            var signingProvider = await _systemConfigService.GetValueAsync(Provider);
            var useClicksign = string.Equals(signingProvider?.Trim(), ProviderClicksign, StringComparison.OrdinalIgnoreCase)
                && await _clicksignService.IsConfiguredAsync();
            var useD4Sign = (!useClicksign || string.Equals(signingProvider?.Trim(), ProviderD4Sign, StringComparison.OrdinalIgnoreCase))
                && await _d4SignService.IsConfiguredAsync();
            var signerEmailAddress = contract.Client.ContactEmail ?? contract.Client.User?.Email;
            if (!string.IsNullOrWhiteSpace(signerEmailAddress))
            {
                if (useClicksign)
                {
                    try
                    {
                        clicksignEnvelopeId = await _clicksignService.SendContractForSignatureAsync(
                            pdfBytes, fileName, signerEmailAddress, signerEmail, message);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Clicksign: erro ao enviar contrato para assinatura: {ex.Message}");
                    }
                }
                else if (useD4Sign)
                {
                    try
                    {
                        d4SignUuid = await _d4SignService.SendContractForSignatureAsync(
                            pdfBytes, fileName, signerEmailAddress, signerEmail, message);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"D4Sign: erro ao enviar contrato para assinatura: {ex.Message}");
                    }
                }
            }

            var generatedAt = DateTime.UtcNow;
            var expiresAt = contract.ExpiryDate;
            var documentEntity = new DocumentModel
            {
                ContractId = contractId,
                Type = DocumentType.Contract,
                FileName = fileName,
                FilePath = filePath,
                Description = description,
                GeneratedAt = generatedAt,
                ExpiresAt = expiresAt,
                D4SignDocumentUuid = d4SignUuid,
                ClicksignEnvelopeIdentifier = clicksignEnvelopeId
            };

            if (regenerate && existingMatch != null)
            {
                existingMatch.FileName = fileName;
                existingMatch.FilePath = filePath;
                existingMatch.GeneratedAt = generatedAt;
                existingMatch.ExpiresAt = expiresAt;
                existingMatch.D4SignDocumentUuid = d4SignUuid;
                existingMatch.ClicksignEnvelopeIdentifier = clicksignEnvelopeId;
                await _context.SaveChangesAsync();
                await _contractHistoryService.RecordContractGeneratedAsync(
                    contractId,
                    contract.ContractNumber,
                    description);
                await _context.SaveChangesAsync();
                return existingMatch;
            }

            var created = await _documentRepository.CreateAsync(documentEntity);
            await _contractHistoryService.RecordContractGeneratedAsync(
                contractId,
                contract.ContractNumber,
                description);
            await _context.SaveChangesAsync();
            return created;
        }

        private static FiscalAddressContractPdfModel BuildFiscalAddressContractPdfModel(
            Contract contract,
            FiscalAddressContractVariant variant)
        {
            var client = contract.Client;
            var coworking = contract.CoworkingAddress.Coworking;
            var address = contract.CoworkingAddress;
            var clientAddress = PdfFieldFormatter.BuildClientAddressLine(
                client.Address,
                client.AddressNumber,
                client.Neighborhood,
                client.City,
                client.State,
                client.ZipCode,
                client.Complement);

            var contractorAddress = PdfFieldFormatter.BuildPartnerContractorAddress(coworking, address);
            var contractorLegalRepresentative = PdfFieldFormatter.ResolveContractorLegalRepresentative(coworking);
            var clientPersonName = PdfFieldFormatter.ResolveClientPersonName(
                client.ContactName,
                client.User?.FullName,
                client.ContactEmail,
                client.User?.Email);

            var signatureCity = !string.IsNullOrWhiteSpace(address.City) && !string.IsNullOrWhiteSpace(address.State)
                ? $"{address.City}/{address.State}"
                : "Cariacica/ES";

            var model = new FiscalAddressContractPdfModel
            {
                Variant = variant,
                ContractNumberFormatted = PdfFieldFormatter.FormatContractNumberForPdf(contract.ContractNumber),
                ClientFullName = clientPersonName,
                ClientCpf = PdfFieldFormatter.FormatCpf(client.CPF),
                ClientAddress = clientAddress,
                ClientEmail = client.ContactEmail ?? client.User?.Email,
                ClientPhone = PdfFieldFormatter.FormatPhone(client.ContactPhone),
                ClientRazaoSocial = client.RazaoSocial ?? client.NomeFantasia,
                ClientCnpj = PdfFieldFormatter.FormatCnpj(client.CNPJ),
                LegalRepresentative = clientPersonName,
                RepresentativeCpf = PdfFieldFormatter.FormatCpf(client.CPF),
                RepresentativeAddress = clientAddress,
                ContractorCompanyName = PdfFieldFormatter.OrDash(coworking.Name),
                ContractorCnpj = PdfFieldFormatter.FormatCnpj(coworking.CNPJ),
                ContractorAddress = contractorAddress,
                PropertyRegistration = PdfFieldFormatter.OrDash(coworking.PropertyRegistration),
                ContractorLegalRepresentative = contractorLegalRepresentative,
                PaymentClause = PdfFieldFormatter.FormatPaymentClause(contract.MonthlyValue, contract.CardPlanType),
                SignatureCity = signatureCity,
                SignatureDate = FiscalAddressContractPdfBuilder.FormatSignatureDate(DateTime.Now),
                ClientSignatureName = variant == FiscalAddressContractVariant.WithCnpj
                    ? (client.RazaoSocial ?? client.NomeFantasia ?? clientPersonName ?? string.Empty)
                    : (clientPersonName ?? string.Empty)
            };

            return model;
        }

        private static byte[] BuildGenericContractPdf(Contract contract)
        {
            using var ms = new MemoryStream();
            var pdfDocument = new PdfDocument(PageSize.A4);
            var writer = PdfWriter.GetInstance(pdfDocument, ms);
            pdfDocument.Open();
            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
            var companyFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
            var coworking = contract.CoworkingAddress.Coworking;
            var title = new Paragraph("CONTRATO DE ENDEREÇO FISCAL", headerFont);
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingAfter = 20;
            pdfDocument.Add(title);
            pdfDocument.Add(new Paragraph($"Empresa: {PdfFieldFormatter.OrDash(coworking.Name)}", companyFont));
            pdfDocument.Add(new Paragraph($"CNPJ: {PdfFieldFormatter.FormatCnpj(coworking.CNPJ)}", companyFont));
            pdfDocument.Add(new Paragraph($"Endereço: {PdfFieldFormatter.BuildPartnerContractorAddress(coworking, contract.CoworkingAddress)}", companyFont));
            pdfDocument.Add(new Paragraph("\n"));
            pdfDocument.Add(new Paragraph($"Número do Contrato: {contract.ContractNumber}", companyFont));
            pdfDocument.Add(new Paragraph($"Data de Início: {contract.StartDate:dd/MM/yyyy}", companyFont));
            pdfDocument.Add(new Paragraph($"Valor Mensal: R$ {contract.MonthlyValue:F2}", companyFont));
            pdfDocument.Add(new Paragraph("\n"));
            pdfDocument.Add(new Paragraph("DADOS DO CONTRATANTE", headerFont));
            if (!string.IsNullOrEmpty(contract.Client.RazaoSocial))
                pdfDocument.Add(new Paragraph($"Razão Social: {contract.Client.RazaoSocial}", companyFont));
            if (!string.IsNullOrEmpty(contract.Client.NomeFantasia))
                pdfDocument.Add(new Paragraph($"Nome Fantasia: {contract.Client.NomeFantasia}", companyFont));
            if (!string.IsNullOrEmpty(contract.Client.CNPJ))
                pdfDocument.Add(new Paragraph($"CNPJ: {contract.Client.CNPJ}", companyFont));
            else if (!string.IsNullOrEmpty(contract.Client.CPF))
                pdfDocument.Add(new Paragraph($"CPF: {contract.Client.CPF}", companyFont));
            if (!string.IsNullOrEmpty(contract.Client.ContactName))
                pdfDocument.Add(new Paragraph($"Nome do Responsável: {contract.Client.ContactName}", companyFont));
            if (!string.IsNullOrEmpty(contract.Client.ContactEmail))
                pdfDocument.Add(new Paragraph($"E-mail: {contract.Client.ContactEmail}", companyFont));
            if (!string.IsNullOrEmpty(contract.Client.ContactPhone))
                pdfDocument.Add(new Paragraph($"Telefone: {contract.Client.ContactPhone}", companyFont));
            var hasClientAddress = !string.IsNullOrEmpty(contract.Client.Address) ||
                                   !string.IsNullOrEmpty(contract.Client.City) ||
                                   !string.IsNullOrEmpty(contract.Client.State);
            if (hasClientAddress)
            {
                pdfDocument.Add(new Paragraph("\nEndereço do Contratante:", companyFont));
                if (!string.IsNullOrEmpty(contract.Client.Address))
                    pdfDocument.Add(new Paragraph($"  {contract.Client.Address}", companyFont));
                var clientCityState = new List<string>();
                if (!string.IsNullOrEmpty(contract.Client.City))
                    clientCityState.Add(contract.Client.City);
                if (!string.IsNullOrEmpty(contract.Client.State))
                    clientCityState.Add(contract.Client.State);
                if (clientCityState.Any())
                    pdfDocument.Add(new Paragraph($"  {string.Join("/", clientCityState)}", companyFont));
                if (!string.IsNullOrEmpty(contract.Client.ZipCode))
                    pdfDocument.Add(new Paragraph($"  CEP: {contract.Client.ZipCode}", companyFont));
            }
            pdfDocument.Add(new Paragraph("\n"));
            pdfDocument.Add(new Paragraph("ENDEREÇO FISCAL CONTRATADO", headerFont));
            pdfDocument.Add(new Paragraph($"Endereço: {contract.CoworkingAddress.Address}, {contract.CoworkingAddress.Number}", companyFont));
            pdfDocument.Add(new Paragraph($"Bairro: {contract.CoworkingAddress.Neighborhood}", companyFont));
            pdfDocument.Add(new Paragraph($"Cidade: {contract.CoworkingAddress.City}/{contract.CoworkingAddress.State}", companyFont));
            pdfDocument.Add(new Paragraph($"CEP: {contract.CoworkingAddress.ZipCode}", companyFont));
            pdfDocument.Add(new Paragraph("\n"));
            pdfDocument.Add(new Paragraph("TERMOS E CONDIÇÕES", headerFont));
            pdfDocument.Add(new Paragraph("1. O endereço fiscal será utilizado exclusivamente para fins de correspondência e registro na Receita Federal.", companyFont));
            pdfDocument.Add(new Paragraph("2. O contrato tem vigência mensal e será renovado automaticamente.", companyFont));
            pdfDocument.Add(new Paragraph("3. O cancelamento pode ser solicitado a qualquer momento.", companyFont));
            pdfDocument.Add(new Paragraph("\n"));
            pdfDocument.Add(new Paragraph("Assinatura do Contratante: _________________________", companyFont));
            pdfDocument.Add(new Paragraph("\n"));
            pdfDocument.Add(new Paragraph("Assinatura da Empresa: _________________________", companyFont));
            pdfDocument.Close();
            return ms.ToArray();
        }
        public async Task<DocumentModel> GenerateReceiptAsync(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");
            var fileName = $"receipt_{payment.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            byte[] pdfBytes;
            using (var ms = new MemoryStream())
            {
                var pdfDocument = new PdfDocument(PageSize.A4);
                var writer = PdfWriter.GetInstance(pdfDocument, ms);
                pdfDocument.Open();
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                var title = new Paragraph("RECIBO DE PAGAMENTO", headerFont);
                title.Alignment = Element.ALIGN_CENTER;
                pdfDocument.Add(title);
                pdfDocument.Add(new Paragraph($"Valor: R$ {payment.Amount:F2}", normalFont));
                pdfDocument.Add(new Paragraph($"Data: {payment.PaidAt:dd/MM/yyyy}", normalFont));
                pdfDocument.Add(new Paragraph($"Método: {payment.Method}", normalFont));
                pdfDocument.Close();
                pdfBytes = ms.ToArray();
            }
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var provider = await _systemConfigService.GetValueAsync(StorageConfigKeys.Provider) ?? FtpDefaults.ProviderName;
            var (folderKey, defaultFolder) = GetContractsFolderConfigByProvider(provider);
            var folder = await _systemConfigService.GetValueAsync(folderKey) ?? defaultFolder;
            var filePath = await storageService.SaveFileAsync(pdfBytes, fileName, folder);
            var contract = payment.Subscription?.Contract;
            var documentEntity = new DocumentModel
            {
                ContractId = contract?.Id ?? 0,
                Type = DocumentType.Receipt,
                FileName = fileName,
                FilePath = filePath,
                Description = $"Recibo de pagamento - {payment.Amount:F2}"
            };
            return await _documentRepository.CreateAsync(documentEntity);
        }
        public async Task<DocumentModel> GenerateTaxDeclarationAsync(int contractId)
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");
            var fileName = $"tax_declaration_{contract.ContractNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            byte[] pdfBytes;
            using (var ms = new MemoryStream())
            {
                var pdfDocument = new PdfDocument(PageSize.A4);
                var writer = PdfWriter.GetInstance(pdfDocument, ms);
                pdfDocument.Open();
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                var title = new Paragraph("DECLARAÇÃO PARA RECEITA FEDERAL", headerFont);
                title.Alignment = Element.ALIGN_CENTER;
                pdfDocument.Add(title);
                pdfDocument.Add(new Paragraph($"Declaro que o endereço {contract.CoworkingAddress.Address}, {contract.CoworkingAddress.Number}, {contract.CoworkingAddress.City}/{contract.CoworkingAddress.State} é utilizado como endereço fiscal da empresa {contract.Client.RazaoSocial ?? contract.Client.NomeFantasia}, CNPJ {contract.Client.CNPJ}.", normalFont));
                pdfDocument.Close();
                pdfBytes = ms.ToArray();
            }
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var provider = await _systemConfigService.GetValueAsync(StorageConfigKeys.Provider) ?? FtpDefaults.ProviderName;
            var (folderKey, defaultFolder) = GetContractsFolderConfigByProvider(provider);
            var folder = await _systemConfigService.GetValueAsync(folderKey) ?? defaultFolder;
            var filePath = await storageService.SaveFileAsync(pdfBytes, fileName, folder);
            var documentEntity = new DocumentModel
            {
                ContractId = contractId,
                Type = DocumentType.TaxDeclaration,
                FileName = fileName,
                FilePath = filePath,
                Description = "Declaração para Receita Federal"
            };
            return await _documentRepository.CreateAsync(documentEntity);
        }
        public async Task<byte[]?> GetDocumentFileAsync(int documentId)
        {
            var document = await _documentRepository.GetByIdAsync(documentId);
            if (document == null)
                return null;
            try
            {
                var storageService = await _storageFactory.GetStorageServiceAsync();
                var fileContent = await storageService.GetFileAsync(document.FilePath);
                if (fileContent != null)
                    return fileContent;
            }
            catch
            {
            }
            if (File.Exists(document.FilePath))
            {
                return await File.ReadAllBytesAsync(document.FilePath);
            }
            return null;
        }

        private byte[]? LoadContractWatermarkBytes() => PdfWatermarkLoader.LoadKlyonLogo(_environment);

        private static (string FolderKey, string DefaultFolder) GetContractsFolderConfigByProvider(string provider)
        {
            var p = provider?.Trim() ?? "";
            if (p.Equals(OracleStorageDefaults.ProviderName, StringComparison.OrdinalIgnoreCase))
                return (OracleStorageConfigKey.ContractsFolder.ToConfigKey(), OracleStorageDefaults.ContractsFolder);
            if (p.Equals(FtpDefaults.ProviderName, StringComparison.OrdinalIgnoreCase))
                return (FtpConfigKey.ContractsFolder.ToConfigKey(), FtpDefaults.ContractsFolder);
            return (AwsS3ConfigKey.ContractsFolder.ToConfigKey(), AwsS3Defaults.ContractsFolder);
        }
    }
}