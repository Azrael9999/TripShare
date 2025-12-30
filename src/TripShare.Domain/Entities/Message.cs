namespace TripShare.Domain.Entities;

public sealed class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ThreadId { get; set; }
    public MessageThread? Thread { get; set; }
    public Guid SenderId { get; set; }
    public string Body { get; set; } = "";
    public bool IsSystem { get; set; }
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}
