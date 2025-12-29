namespace TripShare.Domain.Entities;

public sealed class BookingSegmentAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }

    public Guid TripSegmentId { get; set; }
    public TripSegment? TripSegment { get; set; }

    public int Seats { get; set; }
}
