using KlyonHubPlatform.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    public class ContractDocumentsBackgroundService : BackgroundService
    {
        private readonly ContractDocumentsWorkQueue _workQueue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ContractDocumentsBackgroundService> _logger;

        public ContractDocumentsBackgroundService(
            ContractDocumentsWorkQueue workQueue,
            IServiceProvider serviceProvider,
            ILogger<ContractDocumentsBackgroundService> logger)
        {
            _workQueue = workQueue;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ContractDocumentsBackgroundService iniciado");

            await foreach (var contractId in _workQueue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var ensureService = scope.ServiceProvider.GetRequiredService<IContractDocumentsEnsureService>();
                    var result = await ensureService.EnsureAsync(contractId, stoppingToken);

                    if (result.DeclarationErrorMessage != null || result.ContractErrorMessage != null)
                    {
                        _logger.LogWarning(
                            "Documentos do contrato {ContractId} gerados parcialmente. Declaração: {DeclarationError}. Contrato: {ContractError}",
                            contractId,
                            result.DeclarationErrorMessage ?? "OK",
                            result.ContractErrorMessage ?? "OK");
                    }
                    else
                    {
                        _logger.LogInformation("Documentos do contrato {ContractId} gerados em background", contractId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Falha ao gerar documentos em background para o contrato {ContractId}",
                        contractId);
                }
            }
        }
    }
}
