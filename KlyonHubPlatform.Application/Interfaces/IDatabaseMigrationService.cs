namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IDatabaseMigrationService
    {
        /// <summary>
        /// Obtém lista de migrations pendentes
        /// </summary>
        Task<List<string>> GetPendingMigrationsAsync();

        /// <summary>
        /// Obtém lista de migrations já aplicadas
        /// </summary>
        Task<List<string>> GetAppliedMigrationsAsync();

        /// <summary>
        /// Executa todas as migrations pendentes
        /// </summary>
        Task<MigrationExecutionResult> ExecuteAllPendingMigrationsAsync();

        /// <summary>
        /// Verifica se existem migrations pendentes
        /// </summary>
        Task<bool> HasPendingMigrationsAsync();

        /// <summary>
        /// Obtém informações sobre o estado do banco de dados
        /// </summary>
        Task<DatabaseStatusDto> GetDatabaseStatusAsync();
    }

    public class MigrationExecutionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> AppliedMigrations { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public DateTime ExecutedAt { get; set; }
        public TimeSpan Duration { get; set; }
        
        /// <summary>
        /// Duração em segundos (para facilitar uso no JavaScript)
        /// </summary>
        public double DurationSeconds => Duration.TotalSeconds;
    }

    public class DatabaseStatusDto
    {
        public bool CanConnect { get; set; }
        public int TotalMigrations { get; set; }
        public int AppliedMigrations { get; set; }
        public int PendingMigrations { get; set; }
        public List<string> PendingMigrationsList { get; set; } = new();
        public List<string> AppliedMigrationsList { get; set; } = new();
        public string LastMigration { get; set; } = string.Empty;
        public DateTime? CheckedAt { get; set; }
    }
}
