namespace TripShare.Domain.Entities;

public sealed class TripSegment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }

    public int OrderIndex { get; set; }
    public Guid FromRoutePointId { get; set; }
    public Guid ToRoutePointId { get; set; }

    public decimal Price { get; set; }
    public string Currency { get; set; } = "LKR";

    // concurrency-controlled counters
    public int BookedSeats { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
