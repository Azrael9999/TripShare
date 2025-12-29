namespace TripShare.Domain.Entities;

public sealed class SmsOtpToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string PhoneNumber { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UsedAt { get; set; }

    public bool IsValid => UsedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}
