namespace TripShare.Domain.Entities;

public enum TripStatus
{
    Scheduled = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3
}


public sealed class Trip
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DriverId { get; set; }
    public User? Driver { get; set; }

    public DateTimeOffset DepartureTimeUtc { get; set; }
    public int SeatsTotal { get; set; } = 3;
    public decimal BaseCurrencyRate { get; set; } = 1m; // reserved

    public bool IsPublic { get; set; } = true;
    public string Currency { get; set; } = "LKR";
    public decimal? DefaultPricePerSeat { get; set; } // optional if you want a simple mode too

    public string? Notes { get; set; }

    public TripStatus Status { get; set; } = TripStatus.Scheduled;

    /// <summary>
    /// If true, bookings are automatically accepted (subject to seat availability).
    /// If false, bookings start as Pending and the driver must accept.
    /// </summary>
    public bool InstantBook { get; set; } = false;

    /// <summary>
    /// Booking cutoff minutes before departure. Prevents last-second spam / coordination issues.
    /// </summary>
    public int BookingCutoffMinutes { get; set; } = 60;

    /// <summary>
    /// Optional max minutes a booking can stay Pending before expiring automatically.
    /// </summary>
    public int PendingBookingExpiryMinutes { get; set; } = 30;


    public List<TripRoutePoint> RoutePoints { get; set; } = new();
    public List<TripSegment> Segments { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
