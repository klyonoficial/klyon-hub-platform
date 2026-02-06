namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Status da reserva (salas, etc.)
    /// </summary>
    public enum ReservationStatus
    {
        Pending = 0,
        Confirmed = 1,
        Cancelled = 2,
        Completed = 3,
        NoShow = 4
    }
}
