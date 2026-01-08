namespace TripShare.Application.Contracts;

public sealed record GoogleLoginRequest(string IdToken, string? DeviceToken, string? Timezone, string? Locale);
public sealed record PasswordRegisterRequest(string Email, string Password, string DisplayName, string? Timezone, string? Locale);
public sealed record PasswordLoginRequest(string Email, string Password, string? Timezone, string? Locale);
public sealed record PasswordResetRequest(string Email);
public sealed record PasswordResetConfirm(string Token, string NewPassword);

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
    bool DriverVerified,
    bool IdentityVerified,
    bool PhoneVerified,
    string Role
);

public sealed record SmsOtpRequest(string PhoneNumber);

public sealed record SmsOtpVerifyRequest(string PhoneNumber, string Otp);
