namespace TripShare.Domain.Entities;

public sealed class MessageThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TripId { get; set; }
    public Guid? BookingId { get; set; }
    public Guid ParticipantAId { get; set; }
    public Guid ParticipantBId { get; set; }
    public bool IsClosed { get; set; }
    public string? ClosedReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<Message> Messages { get; set; } = new();
}
