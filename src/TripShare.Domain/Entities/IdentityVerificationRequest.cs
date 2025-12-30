namespace TripShare.Domain.Entities;

public enum IdentityVerificationStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public sealed class IdentityVerificationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string DocumentType { get; set; } = "";
    public string DocumentReference { get; set; } = "";
    public IdentityVerificationStatus Status { get; set; } = IdentityVerificationStatus.Pending;
    public string? ReviewerNote { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? KycProvider { get; set; }
    public string? KycReference { get; set; }
}
