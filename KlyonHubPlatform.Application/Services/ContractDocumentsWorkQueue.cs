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

        public ChannelReader<int> Reader => _channel.Reader;

        public ValueTask EnqueueAsync(int contractId, CancellationToken cancellationToken = default)
            => _channel.Writer.WriteAsync(contractId, cancellationToken);
    }
}
