namespace KlyonHubPlatform.Api.Controllers
{
    public class ExecuteMigrationsRequest
    {
        public bool Confirmed { get; set; }
        public string? BackupConfirmation { get; set; }
    }
}
