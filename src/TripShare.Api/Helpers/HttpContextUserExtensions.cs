using System.Security.Claims;

namespace TripShare.Api.Helpers;

public static class HttpContextUserExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") ?? user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    public static string GetEmail(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email) ?? "";

    public static bool IsEmailVerified(this ClaimsPrincipal user)
        => string.Equals(user.Claims.FirstOrDefault(c => c.Type == "ev")?.Value, "true", StringComparison.OrdinalIgnoreCase);

    public static bool IsPhoneVerified(this ClaimsPrincipal user)
        => string.Equals(user.Claims.FirstOrDefault(c => c.Type == "pv")?.Value, "true", StringComparison.OrdinalIgnoreCase);

    public static bool IsAdmin(this ClaimsPrincipal user)
        => user.IsInRole("admin") || user.IsInRole("superadmin");

    public static bool IsSuperAdmin(this ClaimsPrincipal user)
        => user.IsInRole("superadmin");
}
