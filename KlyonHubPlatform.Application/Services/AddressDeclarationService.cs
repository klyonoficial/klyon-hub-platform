using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Security.Cryptography;
using System.Text;
using PdfDocument = iTextSharp.text.Document;

namespace KlyonHubPlatform.Application.Services
{
    public class AddressDeclarationService : IAddressDeclarationService
    {
        private readonly ApplicationDbContext _context;
        private readonly StorageServiceFactory _storageFactory;
        private readonly ISystemConfigurationService _systemConfigService;

        public AddressDeclarationService(
            ApplicationDbContext context,
            StorageServiceFactory storageFactory,
            ISystemConfigurationService systemConfigService)
        {
            _context = context;
            _storageFactory = storageFactory;
            _systemConfigService = systemConfigService;
        }

        public async Task<AddressDeclaration> GenerateDeclarationAsync(int contractId)
        {
            var contract = await _context.Contracts
                .Include(c => c.Client)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Coworking)
                        .ThenInclude(c => c.Partners)
                .FirstOrDefaultAsync(c => c.Id == contractId);

            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");

            if (contract.Status != Domain.Core.Enums.ContractStatus.Active)
                throw new InvalidOperationException("Apenas contratos ativos podem gerar declarações");

            // Se não tiver ExpiryDate, definir como 1 ano a partir de hoje
            if (contract.ExpiryDate == null)
            {
                contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                await _context.SaveChangesAsync();
            }

            // Buscar última versão da declaração para este contrato
            var lastVersion = await _context.AddressDeclarations
                .Where(d => d.ContractId == contractId)
                .OrderByDescending(d => d.Version)
                .Select(d => d.Version)
                .FirstOrDefaultAsync();

            var newVersion = lastVersion + 1;
            var declarationNumber = $"DEC-{contract.ContractNumber}-V{newVersion:000}";

            // Gerar PDF
            var pdfBytes = GenerateDeclarationPdf(contract, declarationNumber);

            // Gerar assinatura digital (hash SHA256 do conteúdo + metadados)
            var signatureData = $"{declarationNumber}|{contractId}|{contract.ExpiryDate.Value:yyyyMMdd}|{DateTime.UtcNow:yyyyMMddHHmmss}";
            var digitalSignature = GenerateDigitalSignature(signatureData);

            // Salvar PDF
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var provider = await _systemConfigService.GetValueAsync(StorageConfigKeys.Provider) ?? FtpDefaults.ProviderName;
            var (folderKey, defaultFolder) = GetContractsFolderConfigByProvider(provider);
            var folder = await _systemConfigService.GetValueAsync(folderKey) ?? defaultFolder;
            var fileName = $"declaration_{declarationNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            var filePath = await storageService.SaveFileAsync(pdfBytes, fileName, $"{folder}/declarations");

            // Criar entidade
            var declaration = new AddressDeclaration
            {
                ContractId = contractId,
                DeclarationNumber = declarationNumber,
                Version = newVersion,
                StartDate = contract.StartDate,
                EndDate = contract.ExpiryDate.Value,
                FilePath = filePath,
                DigitalSignature = digitalSignature,
                IsValid = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.AddressDeclarations.Add(declaration);
            await _context.SaveChangesAsync();

            return declaration;
        }

        private byte[] GenerateDeclarationPdf(Contract contract, string declarationNumber)
        {
            using var ms = new MemoryStream();
            var pdfDocument = new PdfDocument(PageSize.A4);
            var writer = PdfWriter.GetInstance(pdfDocument, ms);

            pdfDocument.Open();

            var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, Font.BOLD);
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
            var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
            var smallFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

            // Título
            var title = new Paragraph("DECLARAÇÃO DE AUTORIZAÇÃO DE USO DE ENDEREÇO FISCAL", headerFont);
            title.Alignment = Element.ALIGN_CENTER;
            title.SpacingAfter = 30;
            pdfDocument.Add(title);

            // Dados do Coworking
            var coworking = contract.CoworkingAddress.Coworking;
            var coworkingAddress = contract.CoworkingAddress;
            var coworkingFullAddress = $"{coworkingAddress.Address}, {coworkingAddress.Number}";
            if (!string.IsNullOrEmpty(coworkingAddress.Complement))
                coworkingFullAddress += $", {coworkingAddress.Complement}";
            coworkingFullAddress += $", {coworkingAddress.Neighborhood}, {coworkingAddress.City}/{coworkingAddress.State}, CEP {coworkingAddress.ZipCode}";

            // Dados do Cliente (Empresa / Pessoa: razão social, nome fantasia ou nome do cliente)
            var clientName = !string.IsNullOrWhiteSpace(contract.Client.RazaoSocial) ? contract.Client.RazaoSocial
                : !string.IsNullOrWhiteSpace(contract.Client.NomeFantasia) ? contract.Client.NomeFantasia
                : !string.IsNullOrWhiteSpace(contract.Client.ContactName) ? contract.Client.ContactName
                : "Não informado";
            var clientCnpjCpf = !string.IsNullOrEmpty(contract.Client.CNPJ) ? contract.Client.CNPJ : contract.Client.CPF ?? "Não informado";

            // Primeiro parágrafo
            var para1 = new Paragraph();
            para1.Add(new Chunk("Declaramos, para os devidos fins, que o ", normalFont));
            para1.Add(new Chunk(coworking.Name, boldFont));
            para1.Add(new Chunk($", inscrito no CNPJ sob nº ", normalFont));
            para1.Add(new Chunk(coworking.CNPJ, boldFont));
            para1.Add(new Chunk($", com sede à ", normalFont));
            para1.Add(new Chunk(coworkingFullAddress, boldFont));
            para1.Add(new Chunk(", ", normalFont));
            para1.Add(new Chunk("AUTORIZA", boldFont));
            para1.Add(new Chunk(" a empresa/pessoa abaixo identificada a utilizar o referido endereço ", normalFont));
            para1.Add(new Chunk("exclusivamente como ENDEREÇO FISCAL", boldFont));
            para1.Add(new Chunk(".", normalFont));
            para1.SpacingAfter = 20;
            pdfDocument.Add(para1);

            // Dados da empresa/pessoa
            var dadosPara = new Paragraph("Empresa / Pessoa: ", normalFont);
            dadosPara.Add(new Chunk(clientName, boldFont));
            dadosPara.SpacingAfter = 10;
            pdfDocument.Add(dadosPara);

            var dadosCnpjCpf = new Paragraph("CNPJ / CPF: ", normalFont);
            dadosCnpjCpf.Add(new Chunk(clientCnpjCpf, boldFont));
            dadosCnpjCpf.SpacingAfter = 10;
            pdfDocument.Add(dadosCnpjCpf);

            var dadosDataInicial = new Paragraph("Data Inicial: ", normalFont);
            dadosDataInicial.Add(new Chunk(contract.StartDate.ToString("dd/MM/yyyy"), boldFont));
            dadosDataInicial.SpacingAfter = 10;
            pdfDocument.Add(dadosDataInicial);

            var dadosDataFinal = new Paragraph("Data Final: ", normalFont);
            dadosDataFinal.Add(new Chunk(contract.ExpiryDate!.Value.ToString("dd/MM/yyyy"), boldFont));
            dadosDataFinal.SpacingAfter = 20;
            pdfDocument.Add(dadosDataFinal);

            // Segundo parágrafo
            var para2 = new Paragraph("A presente autorização está vinculada ao ", normalFont);
            para2.Add(new Chunk("Contrato de Cessão de Endereço Fiscal", boldFont));
            para2.Add(new Chunk(" firmado entre as partes, sendo válida exclusivamente durante o período acima informado, desde que o contrato permaneça ativo e adimplente.", normalFont));
            para2.SpacingAfter = 20;
            pdfDocument.Add(para2);

            // Esclarecimentos
            pdfDocument.Add(new Paragraph("Esclarecemos que:", boldFont) { SpacingAfter = 10 });

            var list1 = new ListItem("O endereço será utilizado apenas para fins fiscais e cadastrais;", normalFont);
            var list2 = new ListItem("Não caracteriza vínculo societário, empregatício ou comercial entre as partes;", normalFont);
            var list3 = new ListItem("O coworking não se responsabiliza por atividades operacionais, fiscais ou jurídicas exercidas pelo autorizado.", normalFont);

            var list = new List(List.UNORDERED);
            list.Add(list1);
            list.Add(list2);
            list.Add(list3);
            pdfDocument.Add(list);
            
            // Espaçamento após a lista
            pdfDocument.Add(new Paragraph(" ") { SpacingAfter = 20 });

            // Terceiro parágrafo
            var para3 = new Paragraph("Esta declaração é emitida a pedido do interessado para apresentação junto à ", normalFont);
            para3.Add(new Chunk("Receita Federal, Junta Comercial, Prefeitura Municipal e demais órgãos competentes", boldFont));
            para3.Add(new Chunk(".", normalFont));
            para3.SpacingAfter = 20;
            pdfDocument.Add(para3);

            // Data e local
            var cityState = $"{coworkingAddress.City}/{coworkingAddress.State}";
            var currentDate = DateTime.Now;
            var day = currentDate.Day;
            var month = GetMonthName(currentDate.Month);
            var year = currentDate.Year;
            var dataLocal = new Paragraph($"{cityState}, {day} de {month} de {year}.", normalFont);
            dataLocal.SpacingAfter = 40;
            pdfDocument.Add(dataLocal);

            // Assinaturas de todos os sócios
            var partners = coworking.Partners?.Where(p => 
                p.Role != null && (
                    p.Role.ToLower().Contains("sócio") || 
                    p.Role.ToLower().Contains("diretor") ||
                    p.Role.ToLower().Contains("socio")
                )
            ).ToList() ?? new List<CoworkingPartner>();

            // Se não houver sócios cadastrados, usar o nome do coworking como fallback
            if (!partners.Any())
            {
                // Linha de assinatura (usando uma linha horizontal)
                var linha = new Paragraph(new Chunk(new iTextSharp.text.pdf.draw.LineSeparator()));
                linha.SpacingAfter = 10;
                pdfDocument.Add(linha);

                var assinaturaInfo = new Paragraph();
                assinaturaInfo.Add(new Chunk(coworking.Name, boldFont));
                assinaturaInfo.Add(new Chunk("\nCNPJ: ", normalFont));
                assinaturaInfo.Add(new Chunk(coworking.CNPJ, normalFont));
                assinaturaInfo.SpacingAfter = 20;
                pdfDocument.Add(assinaturaInfo);
            }
            else
            {
                // Adicionar assinatura de cada sócio
                for (int i = 0; i < partners.Count; i++)
                {
                    var partner = partners[i];
                    
                    // Linha de assinatura (usando uma linha horizontal)
                    var linha = new Paragraph(new Chunk(new iTextSharp.text.pdf.draw.LineSeparator()));
                    linha.SpacingAfter = 10;
                    pdfDocument.Add(linha);

                    var assinaturaInfo = new Paragraph();
                    assinaturaInfo.Add(new Chunk(partner.Name, boldFont));
                    assinaturaInfo.Add(new Chunk("\nCargo: ", normalFont));
                    assinaturaInfo.Add(new Chunk(partner.Role ?? "Sócio", normalFont));
                    assinaturaInfo.Add(new Chunk("\nCPF: ", normalFont));
                    assinaturaInfo.Add(new Chunk(partner.CPF ?? "Não informado", normalFont));
                    
                    // Adicionar informações da empresa apenas na última assinatura
                    if (i == partners.Count - 1)
                    {
                        assinaturaInfo.Add(new Chunk("\n\n", normalFont));
                        assinaturaInfo.Add(new Chunk(coworking.Name, boldFont));
                        assinaturaInfo.Add(new Chunk("\nCNPJ: ", normalFont));
                        assinaturaInfo.Add(new Chunk(coworking.CNPJ, normalFont));
                    }
                    
                    assinaturaInfo.SpacingAfter = i == partners.Count - 1 ? 20 : 15;
                    pdfDocument.Add(assinaturaInfo);
                }
            }

            // Rodapé com número da declaração e assinatura digital
            var rodape = new Paragraph($"Declaração Nº: {declarationNumber}\nAssinatura Digital: {GenerateDigitalSignature(declarationNumber).Substring(0, 16)}...", smallFont);
            rodape.Alignment = Element.ALIGN_CENTER;
            pdfDocument.Add(rodape);

            pdfDocument.Close();
            return ms.ToArray();
        }

        private string GetMonthName(int month)
        {
            var months = new[] { "janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro" };
            return months[month - 1];
        }

        private string GenerateDigitalSignature(string data)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hashBytes);
        }

        public async Task<byte[]?> GetDeclarationPdfAsync(int declarationId)
        {
            var declaration = await _context.AddressDeclarations
                .FirstOrDefaultAsync(d => d.Id == declarationId);

            if (declaration == null || !declaration.IsValid)
                return null;

            var storageService = await _storageFactory.GetStorageServiceAsync();
            return await storageService.GetFileAsync(declaration.FilePath);
        }

        public async Task<List<AddressDeclaration>> GetDeclarationsByContractAsync(int contractId)
        {
            return await _context.AddressDeclarations
                .Where(d => d.ContractId == contractId)
                .OrderByDescending(d => d.Version)
                .ToListAsync();
        }

        public async Task InvalidateExpiredDeclarationsAsync()
        {
            var expiredDeclarations = await _context.AddressDeclarations
                .Where(d => d.IsValid && d.EndDate < DateTime.UtcNow)
                .ToListAsync();

            foreach (var declaration in expiredDeclarations)
            {
                declaration.IsValid = false;
                declaration.InvalidatedAt = DateTime.UtcNow;
                declaration.InvalidationReason = "Declaração expirada automaticamente";
                declaration.UpdatedAt = DateTime.UtcNow;
            }

            if (expiredDeclarations.Any())
            {
                await _context.SaveChangesAsync();
            }
        }

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
