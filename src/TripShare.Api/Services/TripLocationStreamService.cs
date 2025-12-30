using System.Collections.Concurrent;
using System.Threading.Channels;
using TripShare.Application.Contracts;

namespace TripShare.Api.Services;

public sealed class TripLocationStreamService
{
    private readonly ConcurrentDictionary<Guid, TripLocationStream> _streams = new();

    public async ValueTask PublishAsync(Guid tripId, TripLocationUpdateDto update, CancellationToken ct)
    {
        var stream = _streams.GetOrAdd(tripId, _ => new TripLocationStream());
        stream.SetLatest(update);
        await stream.Writer.WriteAsync(update, ct);
    }

    public TripLocationSubscription Subscribe(Guid tripId, CancellationToken ct)
    {
        var stream = _streams.GetOrAdd(tripId, _ => new TripLocationStream());
        var reader = stream.Reader;
        var latest = stream.Latest;
        return new TripLocationSubscription(latest, reader.ReadAllAsync(ct));
    }

    private sealed class TripLocationStream
    {
        private readonly Channel<TripLocationUpdateDto> _channel = Channel.CreateUnbounded<TripLocationUpdateDto>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        public TripLocationUpdateDto? Latest { get; private set; }
        public ChannelWriter<TripLocationUpdateDto> Writer => _channel.Writer;
        public ChannelReader<TripLocationUpdateDto> Reader => _channel.Reader;

        public void SetLatest(TripLocationUpdateDto update) => Latest = update;
    }
}

public sealed record TripLocationSubscription(TripLocationUpdateDto? Latest, IAsyncEnumerable<TripLocationUpdateDto> Stream);
