using KlyonHubPlatform.Application.Interfaces;
using KlyonHubPlatform.Infra.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace KlyonHubPlatform.Application.Services
{
    public class DatabaseMigrationService : IDatabaseMigrationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseMigrationService> _logger;

        public DatabaseMigrationService(
            ApplicationDbContext context,
            ILogger<DatabaseMigrationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<string>> GetPendingMigrationsAsync()
        {
            try
            {
                var pending = await _context.Database.GetPendingMigrationsAsync();
                return pending.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter migrations pendentes");
                return new List<string>();
            }
        }

        public async Task<List<string>> GetAppliedMigrationsAsync()
        {
            try
            {
                var applied = await _context.Database.GetAppliedMigrationsAsync();
                return applied.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter migrations aplicadas");
                return new List<string>();
            }
        }

        public async Task<bool> HasPendingMigrationsAsync()
        {
            try
            {
                var pending = await _context.Database.GetPendingMigrationsAsync();
                return pending.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar migrations pendentes");
                return false;
            }
        }

        public async Task<MigrationExecutionResult> ExecuteAllPendingMigrationsAsync()
        {
            var result = new MigrationExecutionResult
            {
                ExecutedAt = DateTime.Now
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Iniciando execução de migrations...");

                // Verificar conexão
                var canConnect = await _context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    result.Success = false;
                    result.Message = "Não foi possível conectar ao banco de dados.";
                    result.Errors.Add("Conexão com banco falhou");
                    return result;
                }

                // Obter migrations pendentes
                var pendingMigrations = await GetPendingMigrationsAsync();
                
                if (!pendingMigrations.Any())
                {
                    result.Success = true;
                    result.Message = "Nenhuma migration pendente para executar.";
                    stopwatch.Stop();
                    result.Duration = stopwatch.Elapsed;
                    return result;
                }

                _logger.LogInformation($"Encontradas {pendingMigrations.Count} migrations pendentes");

                // Executar migrations
                await _context.Database.MigrateAsync();

                // Verificar se foram aplicadas
                var stillPending = await GetPendingMigrationsAsync();
                var applied = pendingMigrations.Except(stillPending).ToList();

                result.Success = !stillPending.Any();
                result.AppliedMigrations = applied;
                result.Message = $"✅ {applied.Count} migration(s) executada(s) com sucesso!";

                if (stillPending.Any())
                {
                    result.Errors.Add($"Ainda existem {stillPending.Count} migration(s) pendente(s)");
                }

                _logger.LogInformation($"Migrations executadas com sucesso: {string.Join(", ", applied)}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao executar migrations");
                result.Success = false;
                result.Message = $"❌ Erro ao executar migrations: {ex.Message}";
                result.Errors.Add(ex.Message);
                
                if (ex.InnerException != null)
                {
                    result.Errors.Add($"Inner Exception: {ex.InnerException.Message}");
                }
            }

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            return result;
        }

        public async Task<DatabaseStatusDto> GetDatabaseStatusAsync()
        {
            var status = new DatabaseStatusDto
            {
                CheckedAt = DateTime.Now
            };

            try
            {
                // Testar conexão
                status.CanConnect = await _context.Database.CanConnectAsync();

                if (!status.CanConnect)
                {
                    return status;
                }

                // Obter migrations
                var applied = await GetAppliedMigrationsAsync();
                var pending = await GetPendingMigrationsAsync();
                var all = applied.Union(pending).ToList();

                status.AppliedMigrations = applied.Count;
                status.PendingMigrations = pending.Count;
                status.TotalMigrations = all.Count;
                status.AppliedMigrationsList = applied;
                status.PendingMigrationsList = pending;
                status.LastMigration = applied.LastOrDefault() ?? "Nenhuma";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter status do banco de dados");
            }

            return status;
        }
    }
}
