namespace TripShare.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = "";
    public bool EmailVerified { get; set; }

    public string DisplayName { get; set; } = "";
    public string? PhotoUrl { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneVerified { get; set; }

    public string AuthProvider { get; set; } = "google";
    public string ProviderUserId { get; set; } = ""; // google 'sub'
    public string? PasswordHash { get; set; }
    public string? PasswordSalt { get; set; }

    public bool IsDriver { get; set; }
    public bool DriverVerified { get; set; }
    public DateTimeOffset? DriverVerifiedAt { get; set; }
    public string? DriverVerificationNote { get; set; }
    public bool IdentityVerified { get; set; }
    public DateTimeOffset? IdentityVerifiedAt { get; set; }
    public bool IsSuspended { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string Role { get; set; } = "user"; // user/admin
    public bool AdminApproved { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }

    public Vehicle? Vehicle { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = new();
}
