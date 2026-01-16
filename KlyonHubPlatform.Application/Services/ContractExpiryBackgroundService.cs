using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace KlyonHubPlatform.Application.Services
{
    public class ContractExpiryBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ContractExpiryBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24); // Verificar uma vez por dia

        public ContractExpiryBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<ContractExpiryBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ContractExpiryBackgroundService iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Criar escopo para obter serviços com escopo
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var notificationService = scope.ServiceProvider.GetRequiredService<ContractExpiryNotificationService>();
                        await notificationService.CheckAndSendExpiryNotificationsAsync();
                    }

                    _logger.LogInformation("Verificação de vencimento de contratos concluída. Próxima verificação em 24 horas.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao executar verificação de vencimento de contratos");
                }

                // Aguardar 24 horas antes da próxima verificação
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}
