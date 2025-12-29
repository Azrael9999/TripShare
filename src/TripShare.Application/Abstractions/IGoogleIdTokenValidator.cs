namespace TripShare.Application.Abstractions;

public sealed record GoogleTokenPayload(
    string Sub,
    string Email,
    bool EmailVerified,
    string? Name,
    string? Picture
);

public interface IGoogleIdTokenValidator
{
    Task<GoogleTokenPayload> ValidateAsync(string idToken, string expectedClientId, CancellationToken ct);
}
