using KlyonHubPlatform.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    /// <summary>
    /// Worker diário que cria cobranças PIX Automático no Asaas entre 2 e 10 dias úteis antes do vencimento.
    /// Doc: https://docs.asaas.com/docs/pix-automatico-implementacao
    /// </summary>
    public class PixAutomaticBillingBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PixAutomaticBillingBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);

        public PixAutomaticBillingBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<PixAutomaticBillingBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PixAutomaticBillingBackgroundService iniciado");
            // Aguarda um pouco no startup para não competir com migrations/seed
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
                    var processed = await subscriptionService.ProcessPixAutomaticRenewalsAsync();
                    if (processed.Count > 0)
                    {
                        _logger.LogInformation(
                            "PIX Automático: {Count} cobrança(s) de renovação emitida(s)",
                            processed.Count);
                    }
                    else
                    {
                        _logger.LogDebug("PIX Automático: nenhuma renovação na janela de emissão");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro no PixAutomaticBillingBackgroundService");
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation("PixAutomaticBillingBackgroundService encerrado");
        }
    }
}
