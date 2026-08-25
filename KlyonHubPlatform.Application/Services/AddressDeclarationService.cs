using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Constants;
using KlyonHubPlatform.Domain.Core.Enums;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
namespace KlyonHubPlatform.Application.Services
{
    public class AddressDeclarationService : IAddressDeclarationService
    {
        private readonly ApplicationDbContext _context;
        private readonly StorageServiceFactory _storageFactory;
        private readonly ISystemConfigurationService _systemConfigService;
        private readonly IHostEnvironment _environment;
        public AddressDeclarationService(
            ApplicationDbContext context,
            StorageServiceFactory storageFactory,
            ISystemConfigurationService systemConfigService,
            IHostEnvironment environment)
        {
            _context = context;
            _storageFactory = storageFactory;
            _systemConfigService = systemConfigService;
            _environment = environment;
        }
        public async Task<bool> CanGenerateDeclarationAsync(int contractId, CancellationToken cancellationToken = default)
        {
            var contract = await _context.Contracts
                .Include(c => c.Service)
                .Include(c => c.Subscription)
                .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);
            if (contract == null)
                return false;

            var declarations = await _context.AddressDeclarations
                .Where(d => d.ContractId == contractId)
                .ToListAsync(cancellationToken);

            var hasPaidPayment = contract.Subscription != null
                && await _context.Payments.AnyAsync(
                    p => p.SubscriptionId == contract.Subscription!.Id
                         && p.Status == PaymentStatus.Paid,
                    cancellationToken);

            return AddressDeclarationRules.CanGenerateDeclaration(contract, declarations, hasPaidPayment);
        }

        public async Task<AddressDeclaration> GenerateDeclarationAsync(int contractId)
        {
            var contract = await _context.Contracts
                .Include(c => c.Client)
                    .ThenInclude(cl => cl.User)
                .Include(c => c.Service)
                .Include(c => c.Subscription)
                .Include(c => c.CoworkingAddress)
                    .ThenInclude(a => a.Coworking)
                        .ThenInclude(c => c.Partners)
                .FirstOrDefaultAsync(c => c.Id == contractId);
            if (contract == null)
                throw new InvalidOperationException("Contrato não encontrado");

            var party = ClientContractPartyRules.Evaluate(contract.Client);
            if (!party.IsComplete)
                throw new InvalidOperationException(party.ErrorMessage ?? ClientContractPartyRules.IncompleteMessage);

            var declarations = await _context.AddressDeclarations
                .Where(d => d.ContractId == contractId)
                .ToListAsync();
            var hasPaidPayment = contract.Subscription != null
                && await _context.Payments.AnyAsync(
                    p => p.SubscriptionId == contract.Subscription!.Id
                         && p.Status == PaymentStatus.Paid);

            if (!AddressDeclarationRules.CanGenerateDeclaration(contract, declarations, hasPaidPayment))
                throw new InvalidOperationException(
                    "Não é possível gerar declaração: o contrato precisa estar vigente, com pagamento confirmado e sem declaração válida para o tipo de serviço atual.");

            var validExisting = declarations.FirstOrDefault(d =>
                AddressDeclarationRules.IsDeclarationValidForCurrentService(
                    d,
                    contract.ServiceId,
                    contract.Service?.Code));
            if (validExisting != null)
                return validExisting;

            if (contract.ExpiryDate == null)
            {
                contract.ExpiryDate = DateTime.UtcNow.AddYears(1);
                await _context.SaveChangesAsync();
            }
            var lastVersion = await _context.AddressDeclarations
                .Where(d => d.ContractId == contractId)
                .OrderByDescending(d => d.Version)
                .Select(d => d.Version)
                .FirstOrDefaultAsync();
            var newVersion = lastVersion + 1;
            var declarationNumber = $"DEC-{contract.ContractNumber}-V{newVersion:000}";
            var pdfBytes = GenerateDeclarationPdf(contract, declarationNumber, contract.Service?.Code);
            var signatureData = $"{declarationNumber}|{contractId}|{contract.ExpiryDate.Value:yyyyMMdd}|{DateTime.UtcNow:yyyyMMddHHmmss}";
            var digitalSignature = GenerateDigitalSignature(signatureData);
            var storageService = await _storageFactory.GetStorageServiceAsync();
            var provider = await _systemConfigService.GetValueAsync(StorageConfigKeys.Provider) ?? FtpDefaults.ProviderName;
            var (folderKey, defaultFolder) = GetContractsFolderConfigByProvider(provider);
            var folder = await _systemConfigService.GetValueAsync(folderKey) ?? defaultFolder;
            var fileName = $"declaration_{declarationNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            var filePath = await storageService.SaveFileAsync(pdfBytes, fileName, $"{folder}/declarations");
            var declaration = new AddressDeclaration
            {
                ContractId = contractId,
                ServiceId = contract.ServiceId,
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
        private byte[] GenerateDeclarationPdf(Contract contract, string declarationNumber, string? serviceCode)
        {
            var isCommercial = ServiceCodes.IsCommercialAddress(serviceCode);
            var coworking = contract.CoworkingAddress.Coworking;
            var coworkingAddress = contract.CoworkingAddress;
            var signatureDate = FormatSignatureDate(DateTime.Now);
            var digitalSignature = GenerateDigitalSignature(declarationNumber);

            var signatories = coworking.Partners?
                .Where(p => p.Role != null && (
                    p.Role.Contains("sócio", StringComparison.OrdinalIgnoreCase) ||
                    p.Role.Contains("socio", StringComparison.OrdinalIgnoreCase) ||
                    p.Role.Contains("diretor", StringComparison.OrdinalIgnoreCase)))
                .Select(p => (
                    p.Name,
                    p.Role ?? "Sócio",
                    p.CPF ?? DefaultDisplayStrings.NaoInformado))
                .ToList() ?? [];

            if (signatories.Count == 0 && !string.IsNullOrWhiteSpace(coworking.LegalRepresentative))
            {
                signatories.Add((
                    coworking.LegalRepresentative.Trim(),
                    "Representante Legal",
                    DefaultDisplayStrings.NaoInformado));
            }

            return AddressDeclarationPdfRenderer.Build(new AddressDeclarationPdfRenderOptions
            {
                Title = AddressDeclarationLabels.PdfTitle(serviceCode),
                DeclarationNumber = declarationNumber,
                ContractNumber = contract.ContractNumber,
                ClientName = GetClientDisplayName(contract.Client),
                ClientCnpjCpf = PdfFieldFormatter.FormatCnpjOrCpf(
                    !string.IsNullOrEmpty(contract.Client.CNPJ) ? contract.Client.CNPJ : contract.Client.CPF),
                StartDate = contract.StartDate,
                EndDate = contract.ExpiryDate!.Value,
                IsCommercial = isCommercial,
                CoworkingName = coworking.Name,
                CoworkingCnpj = PdfFieldFormatter.FormatCnpj(coworking.CNPJ),
                CoworkingFullAddress = BuildCoworkingFullAddress(coworkingAddress),
                SignatureCity = $"{coworkingAddress.City}/{coworkingAddress.State}",
                SignatureDate = signatureDate,
                Signatories = signatories,
                DigitalSignaturePreview = digitalSignature[..Math.Min(16, digitalSignature.Length)],
                WatermarkImageBytes = PdfWatermarkLoader.LoadKlyonLogo(_environment)
            });
        }

        private static string FormatSignatureDate(DateTime date)
        {
            var culture = new CultureInfo("pt-BR");
            var month = culture.DateTimeFormat.GetMonthName(date.Month);
            if (!string.IsNullOrEmpty(month))
                month = char.ToUpper(month[0]) + month[1..];
            return $"{date.Day:D2} de {month} de {date.Year}";
        }

        private static string BuildCoworkingFullAddress(CoworkingAddress coworkingAddress)
        {
            var coworkingFullAddress = $"{coworkingAddress.Address}, {coworkingAddress.Number}";
            if (!string.IsNullOrEmpty(coworkingAddress.Complement))
                coworkingFullAddress += $", {coworkingAddress.Complement}";
            coworkingFullAddress += $", {coworkingAddress.Neighborhood}, {coworkingAddress.City}/{coworkingAddress.State}, CEP {coworkingAddress.ZipCode}";
            return coworkingFullAddress;
        }

        private static string GetClientDisplayName(Client client)
        {
            if (!string.IsNullOrWhiteSpace(client.RazaoSocial))
                return client.RazaoSocial;
            if (!string.IsNullOrWhiteSpace(client.NomeFantasia))
                return client.NomeFantasia;

            return PdfFieldFormatter.ResolveClientPersonName(
                client.ContactName,
                client.User?.FullName,
                client.ContactEmail,
                client.User?.Email) ?? string.Empty;
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