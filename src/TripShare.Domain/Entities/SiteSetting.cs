namespace TripShare.Domain.Entities;

/// <summary>
/// Simple key/value settings for platform-wide toggles.
/// </summary>
public sealed class SiteSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
