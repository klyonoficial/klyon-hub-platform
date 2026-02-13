using System.Threading;
using KlyonHubPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static KlyonHubPlatform.Domain.Core.Constants.RoleNames;
using KlyonHubPlatform.Api.Constants;

namespace KlyonHubPlatform.Api.Controllers
{
    [Authorize(Roles = Administrator)]
    [ApiController]
    [Route("api/admin/database")]
    public class DatabaseAdminController : ControllerBase
    {
        private readonly IDatabaseMigrationService _migrationService;
        private readonly ILogger<DatabaseAdminController> _logger;

        public DatabaseAdminController(
            IDatabaseMigrationService migrationService,
            ILogger<DatabaseAdminController> logger)
        {
            _migrationService = migrationService;
            _logger = logger;
        }

        /// <summary>
        /// Obtém status do banco de dados
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetDatabaseStatus(CancellationToken cancellationToken = default)
        {
            try
            {
                var status = await _migrationService.GetDatabaseStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter status do banco");
                return StatusCode(500, new { error = ErrorJson.ErrorDatabaseStatus, details = ex.Message });
            }
        }

        /// <summary>
        /// Lista migrations pendentes
        /// </summary>
        [HttpGet("migrations/pending")]
        public async Task<IActionResult> GetPendingMigrations(CancellationToken cancellationToken = default)
        {
            try
            {
                var pending = await _migrationService.GetPendingMigrationsAsync();
                return Ok(new { count = pending.Count, migrations = pending });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter migrations pendentes");
                return StatusCode(500, new { error = ErrorJson.ErrorPendingMigrations, details = ex.Message });
            }
        }

        /// <summary>
        /// Lista migrations aplicadas
        /// </summary>
        [HttpGet("migrations/applied")]
        public async Task<IActionResult> GetAppliedMigrations(CancellationToken cancellationToken = default)
        {
            try
            {
                var applied = await _migrationService.GetAppliedMigrationsAsync();
                return Ok(new { count = applied.Count, migrations = applied });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter migrations aplicadas");
                return StatusCode(500, new { error = ErrorJson.ErrorAppliedMigrations, details = ex.Message });
            }
        }

        /// <summary>
        /// Executa todas as migrations pendentes
        /// </summary>
        [HttpPost("migrations/execute")]
        public async Task<IActionResult> ExecuteMigrations([FromBody] ExecuteMigrationsRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Validar confirmação
                if (!request.Confirmed)
                {
                    return BadRequest(new { error = ErrorJson.ConfirmationRequiredMigrations });
                }

                _logger.LogWarning($"Usuário {User.Identity?.Name} iniciou execução de migrations");

                var result = await _migrationService.ExecuteAllPendingMigrationsAsync();

                if (result.Success)
                {
                    _logger.LogInformation($"Migrations executadas com sucesso: {string.Join(", ", result.AppliedMigrations)}");
                    return Ok(result);
                }
                else
                {
                    _logger.LogError($"Falha ao executar migrations: {result.Message}");
                    return StatusCode(500, result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro crítico ao executar migrations");
                return StatusCode(500, new { error = ErrorJson.ErrorCriticalMigrations, details = ex.Message });
            }
        }

        /// <summary>
        /// Verifica se há migrations pendentes
        /// </summary>
        [HttpGet("migrations/has-pending")]
        public async Task<IActionResult> HasPendingMigrations(CancellationToken cancellationToken = default)
        {
            try
            {
                var hasPending = await _migrationService.HasPendingMigrationsAsync();
                return Ok(new { hasPending });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar migrations pendentes");
                return StatusCode(500, new { error = ErrorJson.ErrorVerifyMigrations, details = ex.Message });
            }
        }
    }
}
