namespace KlyonHubPlatform.Application.Interfaces
{
    public interface IContractDocumentsWorkQueue
    {
        ValueTask EnqueueAsync(int contractId, CancellationToken cancellationToken = default);
    }
}
