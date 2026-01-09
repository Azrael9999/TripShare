namespace TripShare.Domain.Entities;

public enum RoutePointType
{
    Start = 0,
    Stop = 1,
    End = 2
}

public sealed class TripRoutePoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }

    public int OrderIndex { get; set; }
    public RoutePointType Type { get; set; }

    public double Lat { get; set; }
    public double Lng { get; set; }
    public string DisplayAddress { get; set; } = "";
    public string? PlaceId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
