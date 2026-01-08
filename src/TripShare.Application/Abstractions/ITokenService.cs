using System.Security.Claims;

namespace TripShare.Application.Abstractions;

public interface ITokenService
{
    string CreateAccessToken(Guid userId, string email, string role, bool emailVerified, bool phoneVerified);
    (string refreshToken, string refreshTokenHash) CreateRefreshToken();
    string HashToken(string token);
    ClaimsPrincipal? ValidateAccessToken(string token);
}
