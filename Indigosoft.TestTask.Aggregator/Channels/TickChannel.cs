using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Indigosoft.TestTask.Core.Models;
using Indigosoft.TestTask.Core.Options;
using Microsoft.Extensions.Options;

namespace Indigosoft.TestTask.Aggregator.Channels;

public sealed class TickChannel
{
    private const int DefaultCapacity = 100_000;

    private readonly Channel<NormalizedTick> _channel;
    private long _count;

    public TickChannel(IOptions<TickChannelOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Capacity = options.Value.Capacity > 0
            ? options.Value.Capacity
            : DefaultCapacity;

        _channel = Channel.CreateBounded<NormalizedTick>(
            new BoundedChannelOptions(Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });
    }

    public int Capacity { get; }

    public int Count
    {
        get
        {
            var count = Volatile.Read(ref _count);
            return (int)Math.Clamp(count, 0, int.MaxValue);
        }
    }

    public double FillRatio
    {
        get
        {
            if (Capacity <= 0)
            {
                return 0d;
            }

            return Math.Clamp((double)Count / Capacity, 0d, 1d);
        }
    }

    public async ValueTask EnqueueAsync(
        NormalizedTick tick,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tick);

        await _channel.Writer.WriteAsync(tick, cancellationToken);
        Interlocked.Increment(ref _count);
    }

    public async ValueTask<NormalizedTick> ReadAsync(CancellationToken cancellationToken)
    {
        var tick = await _channel.Reader.ReadAsync(cancellationToken);
        Interlocked.Decrement(ref _count);

        return tick;
    }

    public async IAsyncEnumerable<NormalizedTick> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var tick in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            Interlocked.Decrement(ref _count);
            yield return tick;
        }
    }

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }

    public void Complete(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        _channel.Writer.TryComplete(exception);
    }
}
