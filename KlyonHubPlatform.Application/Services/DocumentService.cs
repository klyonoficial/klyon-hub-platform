using KlyonHubPlatform.Application.Interfaces;
using iTextSharp.text;
using iTextSharp.text.pdf;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Infra.Data.Repositories;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using PdfDocument = iTextSharp.text.Document;
using DocumentModel = KlyonHubPlatform.Domain.Entities.Document;
using KlyonHubPlatform.Domain.Interfaces;
using System.Linq;

namespace KlyonHubPlatform.Application.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _documentRepository;
        private readonly IContractRepository _contractRepository;
        private readonly IPaymentRepository _paymentRepository;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _environment;
        private readonly StorageServiceFactory _storageFactory;
        private readonly ISystemConfigurationService _systemConfigService;

        public DocumentService(
            IDocumentRepository documentRepository,
            IContractRepository contractRepository,
            IPaymentRepository paymentRepository,
            ApplicationDbContext context,
            IConfiguration configuration,
            IHostEnvironment environment,
            StorageServiceFactory storageFactory,
            ISystemConfigurationService systemConfigService)
        {
            _documentRepository = documentRepository;
            _contractRepository = contractRepository;
            _paymentRepository = paymentRepository;
            _context = context;
            _configuration = configuration;
            _environment = environment;
            _storageFactory = storageFactory;
            _systemConfigService = systemConfigService;
        }

        public async Task<DocumentModel> GenerateContractDocumentAsync(int contractId)
        {
            var contract = await _contractRepository.GetByIdAsync(contractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");

            var docSettings = _configuration.GetSection("DocumentSettings");
            var fileName = $"contract_{contract.ContractNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            
            // Gera o PDF em memória
            byte[] pdfBytes;
            using (var ms = new MemoryStream())
            {
                var pdfDocument = new PdfDocument(PageSize.A4);
                var writer = PdfWriter.GetInstance(pdfDocument, ms);

                pdfDocument.Open();

                // Cabeçalho
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                var title = new Paragraph("CONTRATO DE ENDEREÇO FISCAL", headerFont);
                title.Alignment = Element.ALIGN_CENTER;
                title.SpacingAfter = 20;
                pdfDocument.Add(title);

                // Informações da empresa
                var companyFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);
                pdfDocument.Add(new Paragraph($"Empresa: {docSettings["CompanyName"]}", companyFont));
                pdfDocument.Add(new Paragraph($"CNPJ: {docSettings["CompanyCNPJ"]}", companyFont));
                pdfDocument.Add(new Paragraph($"Endereço: {docSettings["CompanyAddress"]}", companyFont));
                pdfDocument.Add(new Paragraph("\n"));

                // Informações do contrato
                pdfDocument.Add(new Paragraph($"Número do Contrato: {contract.ContractNumber}", companyFont));
                pdfDocument.Add(new Paragraph($"Data de Início: {contract.StartDate:dd/MM/yyyy}", companyFont));
                pdfDocument.Add(new Paragraph($"Valor Mensal: R$ {contract.MonthlyValue:F2}", companyFont));
                pdfDocument.Add(new Paragraph("\n"));

                // Informações do cliente
                pdfDocument.Add(new Paragraph("DADOS DO CONTRATANTE", headerFont));
                
                // Razão Social
                if (!string.IsNullOrEmpty(contract.Client.RazaoSocial))
                {
                    pdfDocument.Add(new Paragraph($"Razão Social: {contract.Client.RazaoSocial}", companyFont));
                }
                
                // Nome Fantasia
                if (!string.IsNullOrEmpty(contract.Client.NomeFantasia))
                {
                    pdfDocument.Add(new Paragraph($"Nome Fantasia: {contract.Client.NomeFantasia}", companyFont));
                }
                
                // CNPJ ou CPF
                if (!string.IsNullOrEmpty(contract.Client.CNPJ))
                {
                    pdfDocument.Add(new Paragraph($"CNPJ: {contract.Client.CNPJ}", companyFont));
                }
                else if (!string.IsNullOrEmpty(contract.Client.CPF))
                {
                    pdfDocument.Add(new Paragraph($"CPF: {contract.Client.CPF}", companyFont));
                }
                
                // Nome do Contato/Responsável
                if (!string.IsNullOrEmpty(contract.Client.ContactName))
                {
                    pdfDocument.Add(new Paragraph($"Nome do Responsável: {contract.Client.ContactName}", companyFont));
                }
                
                // Email
                if (!string.IsNullOrEmpty(contract.Client.ContactEmail))
                {
                    pdfDocument.Add(new Paragraph($"E-mail: {contract.Client.ContactEmail}", companyFont));
                }
                
                // Telefone
                if (!string.IsNullOrEmpty(contract.Client.ContactPhone))
                {
                    pdfDocument.Add(new Paragraph($"Telefone: {contract.Client.ContactPhone}", companyFont));
                }
                
                // Endereço do Cliente
                var hasClientAddress = !string.IsNullOrEmpty(contract.Client.Address) || 
                                      !string.IsNullOrEmpty(contract.Client.City) || 
                                      !string.IsNullOrEmpty(contract.Client.State);
                
                if (hasClientAddress)
                {
                    pdfDocument.Add(new Paragraph("\nEndereço do Contratante:", companyFont));
                    
                    if (!string.IsNullOrEmpty(contract.Client.Address))
                    {
                        pdfDocument.Add(new Paragraph($"  {contract.Client.Address}", companyFont));
                    }
                    
                    var clientCityState = new List<string>();
                    if (!string.IsNullOrEmpty(contract.Client.City))
                        clientCityState.Add(contract.Client.City);
                    if (!string.IsNullOrEmpty(contract.Client.State))
                        clientCityState.Add(contract.Client.State);
                    
                    if (clientCityState.Any())
                    {
                        pdfDocument.Add(new Paragraph($"  {string.Join("/", clientCityState)}", companyFont));
                    }
                    
                    if (!string.IsNullOrEmpty(contract.Client.ZipCode))
                    {
                        pdfDocument.Add(new Paragraph($"  CEP: {contract.Client.ZipCode}", companyFont));
                    }
                }
                
                pdfDocument.Add(new Paragraph("\n"));

                // Informações do endereço
                pdfDocument.Add(new Paragraph("ENDEREÇO FISCAL CONTRATADO", headerFont));
                pdfDocument.Add(new Paragraph($"Endereço: {contract.CoworkingAddress.Address}, {contract.CoworkingAddress.Number}", companyFont));
                pdfDocument.Add(new Paragraph($"Bairro: {contract.CoworkingAddress.Neighborhood}", companyFont));
                pdfDocument.Add(new Paragraph($"Cidade: {contract.CoworkingAddress.City}/{contract.CoworkingAddress.State}", companyFont));
                pdfDocument.Add(new Paragraph($"CEP: {contract.CoworkingAddress.ZipCode}", companyFont));
                pdfDocument.Add(new Paragraph("\n"));

                // Termos e condições
                pdfDocument.Add(new Paragraph("TERMOS E CONDIÇÕES", headerFont));
                pdfDocument.Add(new Paragraph("1. O endereço fiscal será utilizado exclusivamente para fins de correspondência e registro na Receita Federal.", companyFont));
                pdfDocument.Add(new Paragraph("2. O contrato tem vigência mensal e será renovado automaticamente.", companyFont));
                pdfDocument.Add(new Paragraph("3. O cancelamento pode ser solicitado a qualquer momento.", companyFont));
                pdfDocument.Add(new Paragraph("\n"));

                // Assinaturas
                pdfDocument.Add(new Paragraph("Assinatura do Contratante: _________________________", companyFont));
                pdfDocument.Add(new Paragraph("\n"));
                pdfDocument.Add(new Paragraph("Assinatura da Empresa: _________________________", companyFont));

                pdfDocument.Close();
                pdfBytes = ms.ToArray();
            }

            // Salva usando o storage service
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var provider = await _systemConfigService.GetValueAsync("Storage.Provider") ?? "FTP";
            var folderKey = provider.ToUpper() == "FTP" ? "FTP.ContractsFolder" : "AWS.S3.ContractsFolder";
            var folder = await _systemConfigService.GetValueAsync(folderKey) ?? "contracts";
            
            var filePath = await storageService.SaveFileAsync(pdfBytes, fileName, folder);

            var documentEntity = new DocumentModel
            {
                ContractId = contractId,
                Type = DocumentType.Contract,
                FileName = fileName,
                FilePath = filePath,
                Description = "Contrato de Endereço Fiscal"
            };

            return await _documentRepository.CreateAsync(documentEntity);
        }

        public async Task<DocumentModel> GenerateReceiptAsync(int paymentId)
        {
            var payment = await _paymentRepository.GetByIdAsync(paymentId);
            if (payment == null)
                throw new InvalidOperationException("Pagamento não encontrado");

            var fileName = $"receipt_{payment.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";

            // Gera o PDF em memória
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

            // Salva usando o storage service
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var provider = await _systemConfigService.GetValueAsync("Storage.Provider") ?? "FTP";
            var folderKey = provider.ToUpper() == "FTP" ? "FTP.ContractsFolder" : "AWS.S3.ContractsFolder";
            var folder = await _systemConfigService.GetValueAsync(folderKey) ?? "contracts";
            
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

            // Gera o PDF em memória
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

            // Salva usando o storage service
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var provider = await _systemConfigService.GetValueAsync("Storage.Provider") ?? "FTP";
            var folderKey = provider.ToUpper() == "FTP" ? "FTP.ContractsFolder" : "AWS.S3.ContractsFolder";
            var folder = await _systemConfigService.GetValueAsync(folderKey) ?? "contracts";
            
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

            // Tenta usar o storage service primeiro
            try
            {
                var storageService = await _storageFactory.GetStorageServiceAsync();
                var fileContent = await storageService.GetFileAsync(document.FilePath);
                if (fileContent != null)
                    return fileContent;
            }
            catch
            {
                // Se falhar, tenta ler do sistema de arquivos local (fallback)
            }

            // Fallback para sistema de arquivos local
            if (File.Exists(document.FilePath))
            {
                return await File.ReadAllBytesAsync(document.FilePath);
            }

            return null;
        }
    }
}
