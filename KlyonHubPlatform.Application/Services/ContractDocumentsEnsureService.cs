using System.Collections.Concurrent;

using KlyonHubPlatform.Application.DTOs.ContractDocuments;

using KlyonHubPlatform.Application.Interfaces;

using Microsoft.Extensions.Logging;



namespace KlyonHubPlatform.Application.Services

{

    public class ContractDocumentsEnsureService : IContractDocumentsEnsureService

    {

        private static readonly ConcurrentDictionary<int, SemaphoreSlim> EnsureLocks = new();



        private readonly IAddressDeclarationService _addressDeclarationService;

        private readonly IDocumentService _documentService;

        private readonly IClientProfileService _clientProfileService;

        private readonly ILogger<ContractDocumentsEnsureService> _logger;



        public ContractDocumentsEnsureService(

            IAddressDeclarationService addressDeclarationService,

            IDocumentService documentService,

            IClientProfileService clientProfileService,

            ILogger<ContractDocumentsEnsureService> logger)

        {

            _addressDeclarationService = addressDeclarationService;

            _documentService = documentService;

            _clientProfileService = clientProfileService;

            _logger = logger;

        }



        public Task<ContractDocumentsEnsureResult> GetStatusAsync(int contractId, CancellationToken cancellationToken = default) =>

            BuildStatusAsync(contractId, cancellationToken);



        public async Task<ContractDocumentsEnsureResult> EnsureAsync(int contractId, CancellationToken cancellationToken = default)

        {

            var gate = EnsureLocks.GetOrAdd(contractId, _ => new SemaphoreSlim(1, 1));

            await gate.WaitAsync(cancellationToken);

            try

            {

                return await EnsureCoreAsync(contractId, cancellationToken);

            }

            finally

            {

                gate.Release();

            }

        }



        private async Task<ContractDocumentsEnsureResult> EnsureCoreAsync(int contractId, CancellationToken cancellationToken)

        {

            var result = await BuildStatusAsync(contractId, cancellationToken);

            if (result.ClientPartyIncomplete)

                return result;



            if (!result.DeclarationManualRetryAvailable && !result.ContractManualRetryAvailable)

                return result;



            if (result.DeclarationManualRetryAvailable)

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



            if (result.ContractManualRetryAvailable)

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



            var refreshed = await BuildStatusAsync(contractId, cancellationToken);

            result.DeclarationManualRetryAvailable = refreshed.DeclarationManualRetryAvailable;

            result.ContractManualRetryAvailable = refreshed.ContractManualRetryAvailable;

            return result;

        }



        private async Task<ContractDocumentsEnsureResult> BuildStatusAsync(int contractId, CancellationToken cancellationToken)

        {

            var result = new ContractDocumentsEnsureResult();



            var party = await _clientProfileService.GetContractPartyStatusByContractIdAsync(contractId, cancellationToken);

            if (!party.IsComplete)

            {

                result.ClientPartyIncomplete = true;

                result.ContractErrorMessage = party.ErrorMessage;

                result.DeclarationErrorMessage = party.ErrorMessage;

                _logger.LogWarning(

                    "Contrato {ContractId} sem dados essenciais do contratante. {Message}",

                    contractId,

                    party.ErrorMessage);

                return result;

            }



            if (await _addressDeclarationService.CanGenerateDeclarationAsync(contractId, cancellationToken))

                result.DeclarationManualRetryAvailable = true;



            if (await _documentService.CanGenerateContractDocumentAsync(contractId))

                result.ContractManualRetryAvailable = true;



            return result;

        }

    }

}


