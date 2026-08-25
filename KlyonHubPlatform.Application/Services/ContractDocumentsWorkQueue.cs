using System.Collections.Concurrent;
using System.Threading.Channels;
using KlyonHubPlatform.Application.Interfaces;

namespace KlyonHubPlatform.Application.Services
{
    public sealed class ContractDocumentsWorkQueue : IContractDocumentsWorkQueue
    {
        private readonly Channel<int> _channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        private readonly ConcurrentDictionary<int, byte> _inFlight = new();

        public ChannelReader<int> Reader => _channel.Reader;

        public ValueTask EnqueueAsync(int contractId, CancellationToken cancellationToken = default)
        {
            if (!_inFlight.TryAdd(contractId, 0))
                return ValueTask.CompletedTask;

            return _channel.Writer.WriteAsync(contractId, cancellationToken);
        }

        internal void MarkCompleted(int contractId) => _inFlight.TryRemove(contractId, out _);
    }
}
