namespace TripShare.Domain.Entities;

public sealed class Vehicle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public User? Owner { get; set; }

    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public string Color { get; set; } = "";
    /// <summary>
    /// Plate number (store partial or masked if you prefer)
    /// </summary>
    public string? PlateNumber { get; set; }
    public int Seats { get; set; } = 4;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
