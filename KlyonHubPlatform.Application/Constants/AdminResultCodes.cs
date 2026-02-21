namespace KlyonHubPlatform.Application.Constants
{
    /// <summary>Códigos de resultado retornados pelos métodos do AdminDbService (usados em branching, não mensagens ao usuário).</summary>
    public static class AdminResultCodes
    {
        public const string NoPendingMigrations = "NoPendingMigrations";
        public const string MigrationMarkedAsApplied = "MigrationMarkedAsApplied";
        public const string PartnerServiceNotFound = "PartnerServiceNotFound";
        public const string ServiceAlreadyRegisteredForPartner = "ServiceAlreadyRegisteredForPartner";
    }
}
