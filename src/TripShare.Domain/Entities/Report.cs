namespace TripShare.Domain.Entities;

public enum ReportTargetType
{
    User = 0,
    Trip = 1,
    Booking = 2
}

public enum ReportStatus
{
    Open = 0,
    InReview = 1,
    Resolved = 2,
    Rejected = 3
}

public sealed class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterUserId { get; set; }
    public Guid? TargetUserId { get; set; }
    public Guid? TripId { get; set; }
    public Guid? BookingId { get; set; }

    public ReportTargetType TargetType { get; set; }
    public string Reason { get; set; } = "";
    public string? Details { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public string? AdminNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
