namespace KlyonHubPlatform.Api.Requests
{
    public class ExecuteMigrationsRequest
    {
        public bool Confirmed { get; set; }
        public string? BackupConfirmation { get; set; }
    }
}
