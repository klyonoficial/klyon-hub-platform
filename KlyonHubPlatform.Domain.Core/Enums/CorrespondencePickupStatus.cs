namespace KlyonHubPlatform.Domain.Core.Enums
{
    /// <summary>
    /// Status da retirada da correspondência pelo cliente.
    /// </summary>
    public enum CorrespondencePickupStatus
    {
        /// <summary>Aguardando o cliente agendar data/hora de retirada.</summary>
        AwaitingSchedule = 0,
        /// <summary>Cliente agendou data e hora para retirada.</summary>
        Scheduled = 1,
        /// <summary>Cliente já retirou a correspondência.</summary>
        PickedUp = 2
    }
}
