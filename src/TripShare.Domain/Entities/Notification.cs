namespace TripShare.Domain.Entities;

public enum NotificationType
{
    BookingRequested = 0,
    BookingAccepted = 1,
    BookingRejected = 2,
    BookingCancelled = 3,
    TripUpdated = 4,
    TripCancelled = 5,
    TripStarted = 6,
    TripCompleted = 7,
    System = 99
}

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public NotificationType Type { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";

    public Guid? TripId { get; set; }
    public Guid? BookingId { get; set; }

    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}
