using System.Net.Http.Json;
using TripShare.Application.Abstractions;

namespace TripShare.Api.Services;

/// <summary>
/// Production-friendly validator using Google's tokeninfo endpoint.
/// This keeps implementation simple and correct; for high-scale you can swap to JWKS signature validation.
/// </summary>
public sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    private readonly HttpClient _http = new();

    public async Task<GoogleTokenPayload> ValidateAsync(string idToken, string expectedClientId, CancellationToken ct)
    {
        var url = $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}";
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException("Invalid Google ID token.");

        var data = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: ct)
                   ?? throw new InvalidOperationException("Invalid Google ID token response.");

        if (!data.TryGetValue("aud", out var aud) || aud != expectedClientId)
            throw new InvalidOperationException("Google token audience mismatch.");

        var sub = data.GetValueOrDefault("sub") ?? throw new InvalidOperationException("Google token missing sub.");
        var email = data.GetValueOrDefault("email") ?? throw new InvalidOperationException("Google token missing email.");
        var emailVerified = (data.GetValueOrDefault("email_verified") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        var name = data.GetValueOrDefault("name");
        var picture = data.GetValueOrDefault("picture");

        return new GoogleTokenPayload(sub, email, emailVerified, name, picture);
    }
}
