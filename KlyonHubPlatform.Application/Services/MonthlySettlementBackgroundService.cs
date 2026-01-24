using KlyonHubPlatform.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KlyonHubPlatform.Application.Services
{
    /// <summary>
    /// Background service para processamento automático de fechamentos mensais
    /// - Fecha meses anteriores no primeiro dia de cada mês
    /// - Verifica prazos de NF expirados
    /// - Executa transferências agendadas
    /// </summary>
    public class MonthlySettlementBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MonthlySettlementBackgroundService> _logger;
        
        // Intervalo de verificação: 1 hora
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

        public MonthlySettlementBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<MonthlySettlementBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MonthlySettlementBackgroundService iniciado");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessSettlementsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro no processamento de fechamentos mensais");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("MonthlySettlementBackgroundService encerrado");
        }

        private async Task ProcessSettlementsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var settlementService = scope.ServiceProvider.GetRequiredService<IMonthlySettlementService>();

            var now = DateTime.UtcNow;

            // 1. No primeiro dia do mês, fechar meses anteriores
            if (now.Day == 1 && now.Hour >= 6 && now.Hour <= 8) // Entre 6h e 8h
            {
                _logger.LogInformation("Executando fechamento de meses anteriores");
                await settlementService.CloseAllOpenMonthsAsync();
            }

            // 2. Verificar NFs com prazo expirado (executar sempre)
            _logger.LogDebug("Verificando NFs com prazo expirado");
            await settlementService.ProcessExpiredInvoiceDeadlinesAsync();

            // 3. Processar transferências agendadas para hoje
            var today = now.Date;
            var isBusinessDay = now.DayOfWeek != DayOfWeek.Saturday && now.DayOfWeek != DayOfWeek.Sunday;
            
            if (isBusinessDay && now.Hour >= 10 && now.Hour <= 18) // Horário comercial
            {
                _logger.LogDebug("Processando transferências agendadas");
                await settlementService.ProcessScheduledTransfersAsync();
            }
        }
    }
}
