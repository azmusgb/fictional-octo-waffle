using System.Threading.Channels;

namespace WorkbenchStudio.Api.Services;

public interface IImportQueue
{
    ValueTask QueueAsync(Guid importId, CancellationToken cancellationToken = default);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}

public sealed class ImportQueue : IImportQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
        AllowSynchronousContinuations = false
    });

    public ValueTask QueueAsync(Guid importId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(importId, cancellationToken);

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
