namespace TripShare.Domain.Entities;

public enum SafetyIncidentStatus
{
    Open = 0,
    Escalated = 1,
    Resolved = 2
}

public enum SafetyIncidentType
{
    Panic = 0,
    SafetyConcern = 1,
    Medical = 2,
    Harassment = 3
}

public sealed class SafetyIncident
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? TripId { get; set; }
    public Guid? BookingId { get; set; }
    public SafetyIncidentType Type { get; set; }
    public string Summary { get; set; } = "";
    public SafetyIncidentStatus Status { get; set; } = SafetyIncidentStatus.Open;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EscalatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
}
