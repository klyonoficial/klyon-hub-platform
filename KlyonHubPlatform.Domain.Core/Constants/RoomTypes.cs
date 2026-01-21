namespace KlyonHubPlatform.Domain.Core.Constants
{
    /// <summary>
    /// Tipos de sala para aluguel no marketplace.
    /// </summary>
    public static class RoomTypes
    {
        public const string Shared = "Shared";       // Sala compartilhada
        public const string Private = "Private";     // Sala privativa
        public const string Auditorium = "Auditorium"; // Auditório

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
