using FluentValidation;
using KlyonHubPlatform.Application.DTOs.ClientProfile;
using KlyonHubPlatform.Application.Helpers;
using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Domain.Core.Helpers;
using KlyonHubPlatform.Domain.Entities;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KlyonHubPlatform.Application.Services
{
    public class ClientProfileService : IClientProfileService
    {
        private static readonly string[] AllowedLogoExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

        private readonly ApplicationDbContext _context;
        private readonly IValidator<ClientProfileSaveInput> _validator;
        private readonly IClientAppService _clientAppService;
        private readonly StorageServiceFactory _storageFactory;

        public ClientProfileService(
            ApplicationDbContext context,
            IValidator<ClientProfileSaveInput> validator,
            IClientAppService clientAppService,
            StorageServiceFactory storageFactory)
        {
            _context = context;
            _validator = validator;
            _clientAppService = clientAppService;
            _storageFactory = storageFactory;
        }

        public bool ClientContractPartyIsComplete(Client? client) =>
            ClientContractPartyRules.ClientContractPartyIsComplete(client);

        public async Task<ClientContractPartyStatus> GetContractPartyStatusByUserIdAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            var client = await _context.Clients
                .AsNoTracking()
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
            return ClientContractPartyRules.Evaluate(client);
        }

        public async Task<ClientContractPartyStatus> GetContractPartyStatusByContractIdAsync(
            int contractId,
            CancellationToken cancellationToken = default)
        {
            var contract = await _context.Contracts
                .AsNoTracking()
                .Include(c => c.Client)
                    .ThenInclude(cl => cl!.User)
                .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);
            return ClientContractPartyRules.Evaluate(contract?.Client);
        }

        public async Task<ClientProfileSaveResult> SaveProfileAsync(
            ClientProfileSaveInput input,
            CancellationToken cancellationToken = default)
        {
            var validation = await _validator.ValidateAsync(input, cancellationToken);
            if (!validation.IsValid)
            {
                var first = validation.Errors[0].ErrorMessage;
                return ClientProfileSaveResult.Fail(first);
            }

            var client = await _context.Clients
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == input.UserId, cancellationToken);
            if (client == null)
                return ClientProfileSaveResult.Fail("Cliente não encontrado.");

            var isPj = ClientContractPartyRules.IsPessoaJuridica(input.CNPJ);
            var cnpjFormatted = isPj ? BrazilianTaxId.FormatCnpj(input.CNPJ) : null;
            var cpfDigits = BrazilianTaxId.Digits(input.CPF);
            var cpfFormatted = cpfDigits.Length == 11 ? BrazilianTaxId.FormatCpf(input.CPF) : null;

            if (cnpjFormatted != null)
            {
                var cnpjClean = BrazilianTaxId.Digits(cnpjFormatted);
                if (await _clientAppService.ClientExistsWithCnpjAsync(cnpjClean, client.Id, cancellationToken))
                    return ClientProfileSaveResult.Fail($"O CNPJ {cnpjFormatted} já está cadastrado no sistema.");
            }

            if (cpfFormatted != null)
            {
                var cpfClean = BrazilianTaxId.Digits(cpfFormatted);
                if (await _clientAppService.ClientExistsWithCpfAsync(cpfClean, client.Id, cancellationToken))
                    return ClientProfileSaveResult.Fail($"O CPF {cpfFormatted} já está cadastrado no sistema.");
            }

            var email = BrazilianText.NormalizeEmail(
                string.IsNullOrWhiteSpace(input.AccountEmail) ? client.User?.Email : input.AccountEmail);
            var contactName = BrazilianText.NormalizePersonName(
                ClientDisplayName.NullIfPlaceholder(input.ContactName, email));

            client.RazaoSocial = BrazilianText.NormalizePersonName(input.RazaoSocial);
            client.NomeFantasia = BrazilianText.NormalizePersonName(input.NomeFantasia);
            client.CNPJ = cnpjFormatted;
            client.CPF = cpfFormatted;
            client.ContactName = contactName;
            client.ContactEmail = email;
            client.ContactPhone = BrazilianTaxId.Digits(input.ContactPhone);
            client.ZipCode = BrazilianTaxId.Digits(input.ZipCode);
            client.Address = BrazilianText.NormalizePersonName(input.Address);
            client.AddressNumber = NullIfBlank(input.AddressNumber);
            client.Neighborhood = BrazilianText.NormalizePersonName(input.Neighborhood);
            client.Complement = BrazilianText.NormalizePersonName(input.Complement);
            client.City = BrazilianText.NormalizePersonName(input.City);
            client.State = BrazilianText.NormalizeUf(input.State)!;
            client.UpdatedAt = DateTime.UtcNow;

            if (client.User != null)
            {
                var userName = contactName ?? client.RazaoSocial ?? string.Empty;
                client.User.FullName = userName;
                client.User.UpdatedAt = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(client.ContactPhone))
                    client.User.Phone = client.ContactPhone;
            }

            if (input.Logo != null && input.Logo.Content.Length > 0)
            {
                var logoError = await SaveLogoAsync(client, input, cancellationToken);
                if (logoError != null)
                    return ClientProfileSaveResult.Fail(logoError);
            }

            try
            {
                var asaasWarning = await _clientAppService.SaveClientProfileAsync(client.Id, input.UserId, cancellationToken);
                var displayName = ClientDisplayName.Resolve(
                    contactName ?? client.RazaoSocial,
                    email);
                return ClientProfileSaveResult.Ok(client.ContactName, displayName, asaasWarning);
            }
            catch (DbUpdateException)
            {
                return ClientProfileSaveResult.Fail("Este CNPJ ou CPF já está cadastrado no sistema. Verifique os dados informados.");
            }
        }

        private async Task<string?> SaveLogoAsync(
            Client client,
            ClientProfileSaveInput input,
            CancellationToken cancellationToken)
        {
            var logo = input.Logo!;
            var extension = Path.GetExtension(logo.FileName).ToLowerInvariant();
            if (!AllowedLogoExtensions.Contains(extension))
                return "Formato de arquivo não permitido. Use JPG, PNG, GIF ou WEBP.";
            if (logo.Content.Length > input.MaxLogoBytes)
            {
                var maxMb = (int)(input.MaxLogoBytes / (1024 * 1024));
                return $"O arquivo é muito grande. O tamanho máximo é {maxMb}MB.";
            }

            var storage = await _storageFactory.GetStorageServiceAsync();
            var fileName = $"logo_{client.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
            var oldPath = client.LogoPath;
            client.LogoPath = await storage.SaveFileAsync(logo.Content, fileName, $"clients/{client.Id}");
            if (!string.IsNullOrEmpty(oldPath))
            {
                try { await storage.DeleteFileAsync(oldPath); }
                catch { /* ignora falha ao excluir logo antiga */ }
            }
            return null;
        }

        private static string? NullIfBlank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
