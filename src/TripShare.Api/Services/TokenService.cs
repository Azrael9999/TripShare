using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TripShare.Application.Abstractions;

namespace TripShare.Api.Services;

public sealed class TokenService : ITokenService
{
    private readonly IConfiguration _cfg;
    private readonly JwtSecurityTokenHandler _handler = new();
    private readonly SymmetricSecurityKey _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessMinutes;

    public TokenService(IConfiguration cfg)
    {
        _cfg = cfg;
        var jwt = cfg.GetSection("Jwt");
        _issuer = jwt["Issuer"] ?? "HopTrip";
        _audience = jwt["Audience"] ?? "HopTrip";
        _accessMinutes = int.TryParse(jwt["AccessTokenMinutes"], out var m) ? m : 30;

        var signingKey = jwt["SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey missing");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    }

    public string CreateAccessToken(Guid userId, string email, string role, bool emailVerified, bool phoneVerified)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Role, role),
            new("ev", emailVerified ? "true" : "false"),
            new("pv", phoneVerified ? "true" : "false")
        };

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_accessMinutes),
            signingCredentials: creds);

        return _handler.WriteToken(token);
    }

    public (string refreshToken, string refreshTokenHash) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Base64UrlEncoder.Encode(bytes);
        return (token, HashToken(token));
    }

    public string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        try
        {
            var jwt = _cfg.GetSection("Jwt");
            var signingKey = jwt["SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey missing");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt["Issuer"],
                ValidAudience = jwt["Audience"],
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            return _handler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            return null;
        }
    }
}
