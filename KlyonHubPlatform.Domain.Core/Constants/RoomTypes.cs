namespace KlyonHubPlatform.Domain.Core.Constants
{
    public static class RoomTypes
    {
        public const string Shared = "Shared";
        public const string Private = "Private";
        public const string Auditorium = "Auditorium";
        public static string[] All => new[] { Shared, Private, Auditorium };
        public static string GetDisplayName(string value) => value switch
        {
            Shared => "Sala compartilhada",
            Private => "Sala privativa",
            Auditorium => "Auditório",
            _ => value
        };
    }
}