namespace TripShare.Domain.Entities;

public sealed class EmergencyContact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public bool ShareLiveTripsByDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
