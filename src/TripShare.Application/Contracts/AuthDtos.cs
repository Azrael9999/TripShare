namespace TripShare.Application.Contracts;

public sealed record GoogleLoginRequest(string IdToken, string? DeviceToken, string? Timezone, string? Locale);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    bool RequiresEmailVerification,
    bool IsSuspended,
    UserMeDto Me
);

public sealed record RefreshRequest(string RefreshToken);

public sealed record UserMeDto(
    Guid Id,
    string Email,
    bool EmailVerified,
    string DisplayName,
    string? PhotoUrl,
    bool IsDriver,
    string Role
);
