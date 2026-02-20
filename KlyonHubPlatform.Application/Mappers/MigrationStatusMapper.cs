using KlyonHubPlatform.Application.Interfaces;
namespace KlyonHubPlatform.Application.Mappers
{
    public static class MigrationStatusMapper
    {
        public static DatabaseStatusDto ToDto(
            bool canConnect,
            DateTime? checkedAt,
            IReadOnlyList<string> applied,
            IReadOnlyList<string> pending)
        {
            var all = applied.Union(pending).ToList();
            return new DatabaseStatusDto
            {
                CanConnect = canConnect,
                CheckedAt = checkedAt,
                AppliedMigrations = applied.Count,
                PendingMigrations = pending.Count,
                TotalMigrations = all.Count,
                AppliedMigrationsList = applied.ToList(),
                PendingMigrationsList = pending.ToList(),
                LastMigration = applied.Count > 0 ? applied[^1] : "Nenhuma"
            };
        }
    }
}