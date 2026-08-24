using KlyonHubPlatform.Application.DTOs.ContractDocuments;
using KlyonHubPlatform.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    public class ContractDocumentsEnsureService : IContractDocumentsEnsureService
    {
        private readonly IAddressDeclarationService _addressDeclarationService;
        private readonly IDocumentService _documentService;
        private readonly ILogger<ContractDocumentsEnsureService> _logger;

        public ContractDocumentsEnsureService(
            IAddressDeclarationService addressDeclarationService,
            IDocumentService documentService,
            ILogger<ContractDocumentsEnsureService> logger)
        {
            _addressDeclarationService = addressDeclarationService;
            _documentService = documentService;
            _logger = logger;
        }

        public async Task<ContractDocumentsEnsureResult> EnsureAsync(int contractId, CancellationToken cancellationToken = default)
        {
            var result = new ContractDocumentsEnsureResult();

            if (await _addressDeclarationService.CanGenerateDeclarationAsync(contractId, cancellationToken))
            {
                try
                {
                    await _addressDeclarationService.GenerateDeclarationAsync(contractId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Falha ao gerar declaração automaticamente na abertura do contrato. ContractId: {ContractId}",
                        contractId);
                    result.DeclarationErrorMessage = ex.Message;
                }
            }

            if (await _addressDeclarationService.CanGenerateDeclarationAsync(contractId, cancellationToken))
                result.DeclarationManualRetryAvailable = true;

            if (await _documentService.CanGenerateContractDocumentAsync(contractId))
            {
                try
                {
                    await _documentService.EnsureContractDocumentAsync(contractId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Falha ao gerar contrato automaticamente na abertura do contrato. ContractId: {ContractId}",
                        contractId);
                    result.ContractErrorMessage = ex.Message;
                }
            }

            if (await _documentService.CanGenerateContractDocumentAsync(contractId))
                result.ContractManualRetryAvailable = true;

            return result;
        }
    }
}
