namespace TripShare.Domain.Entities;

public enum BookingStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Cancelled = 3,
    Completed = 4
}

public sealed class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TripId { get; set; }
    public Trip? Trip { get; set; }

    public Guid PassengerId { get; set; }
    public User? Passenger { get; set; }

    public Guid PickupRoutePointId { get; set; }
    public Guid DropoffRoutePointId { get; set; }

    public int Seats { get; set; } = 1;

    // snapshot price at booking time (per seat total)
    public decimal PriceTotal { get; set; }
    public string Currency { get; set; } = "LKR";

    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTimeOffset? PendingExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool ContactRevealed { get; set; }
    public string? CancellationReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<BookingSegmentAllocation> SegmentAllocations { get; set; } = new();
}
