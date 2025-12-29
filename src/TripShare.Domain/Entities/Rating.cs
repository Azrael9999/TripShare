namespace TripShare.Domain.Entities;

public sealed class Rating
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }

    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }

    public int Stars { get; set; } // 1..5
    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
