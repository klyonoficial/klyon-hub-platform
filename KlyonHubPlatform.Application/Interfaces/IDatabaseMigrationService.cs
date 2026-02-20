namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IDatabaseMigrationService
    {
        Task<List<string>> GetPendingMigrationsAsync();
        Task<List<string>> GetAppliedMigrationsAsync();
        Task<MigrationExecutionResult> ExecuteAllPendingMigrationsAsync();
        Task<bool> HasPendingMigrationsAsync();
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