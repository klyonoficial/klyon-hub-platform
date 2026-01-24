using KlyonHubPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KlyonHubPlatform.Api.Controllers
{
    [Authorize(Roles = "Admin")]
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
        public async Task<IActionResult> GetDatabaseStatus()
        {
            try
            {
                var status = await _migrationService.GetDatabaseStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter status do banco");
                return StatusCode(500, new { error = "Erro ao obter status do banco de dados", details = ex.Message });
            }
        }

        /// <summary>
        /// Lista migrations pendentes
        /// </summary>
        [HttpGet("migrations/pending")]
        public async Task<IActionResult> GetPendingMigrations()
        {
            try
            {
                var pending = await _migrationService.GetPendingMigrationsAsync();
                return Ok(new { count = pending.Count, migrations = pending });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter migrations pendentes");
                return StatusCode(500, new { error = "Erro ao obter migrations pendentes", details = ex.Message });
            }
        }

        /// <summary>
        /// Lista migrations aplicadas
        /// </summary>
        [HttpGet("migrations/applied")]
        public async Task<IActionResult> GetAppliedMigrations()
        {
            try
            {
                var applied = await _migrationService.GetAppliedMigrationsAsync();
                return Ok(new { count = applied.Count, migrations = applied });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter migrations aplicadas");
                return StatusCode(500, new { error = "Erro ao obter migrations aplicadas", details = ex.Message });
            }
        }

        /// <summary>
        /// Executa todas as migrations pendentes
        /// </summary>
        [HttpPost("migrations/execute")]
        public async Task<IActionResult> ExecuteMigrations([FromBody] ExecuteMigrationsRequest request)
        {
            try
            {
                // Validar confirmação
                if (!request.Confirmed)
                {
                    return BadRequest(new { error = "Confirmação necessária para executar migrations" });
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
                return StatusCode(500, new { error = "Erro crítico ao executar migrations", details = ex.Message });
            }
        }

        /// <summary>
        /// Verifica se há migrations pendentes
        /// </summary>
        [HttpGet("migrations/has-pending")]
        public async Task<IActionResult> HasPendingMigrations()
        {
            try
            {
                var hasPending = await _migrationService.HasPendingMigrationsAsync();
                return Ok(new { hasPending });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao verificar migrations pendentes");
                return StatusCode(500, new { error = "Erro ao verificar migrations", details = ex.Message });
            }
        }
    }

    public class ExecuteMigrationsRequest
    {
        public bool Confirmed { get; set; }
        public string? BackupConfirmation { get; set; }
    }
}
