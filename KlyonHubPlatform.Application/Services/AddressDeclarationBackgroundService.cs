using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using KlyonHubPlatform.Application.Interfaces;

namespace KlyonHubPlatform.Application.Services
{
    public class AddressDeclarationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AddressDeclarationBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24); // Verificar uma vez por dia

        public AddressDeclarationBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<AddressDeclarationBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AddressDeclarationBackgroundService iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Criar escopo para obter serviços com escopo
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var declarationService = scope.ServiceProvider.GetRequiredService<IAddressDeclarationService>();
                        await declarationService.InvalidateExpiredDeclarationsAsync();
                    }

                    _logger.LogInformation("Verificação de declarações expiradas concluída. Próxima verificação em 24 horas.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao executar verificação de declarações expiradas");
                }

                // Aguardar 24 horas antes da próxima verificação
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
    }
}
