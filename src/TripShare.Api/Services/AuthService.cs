using Microsoft.EntityFrameworkCore;
using TripShare.Application.Abstractions;
using TripShare.Application.Contracts;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Services;

public sealed class AuthService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IGoogleIdTokenValidator _google;
    private readonly IEmailSender _email;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AuthService> _log;
    private readonly IHttpContextAccessor _http;

    public AuthService(
        AppDbContext db,
        ITokenService tokens,
        IGoogleIdTokenValidator google,
        IEmailSender email,
        IConfiguration cfg,
        ILogger<AuthService> log,
        IHttpContextAccessor http)
    {
        _db = db;
        _tokens = tokens;
        _google = google;
        _email = email;
        _cfg = cfg;
        _log = log;
        _http = http;
    }

    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest req, CancellationToken ct)
    {
        var clientId = _cfg["Auth:Google:ClientId"] ?? throw new InvalidOperationException("Auth:Google:ClientId missing");
        var payload = await _google.ValidateAsync(req.IdToken, clientId, ct);

        var user = await _db.Users.SingleOrDefaultAsync(x => x.AuthProvider == "google" && x.ProviderUserId == payload.Sub, ct);
        if (user is null)
        {
            user = await _db.Users.SingleOrDefaultAsync(x => x.Email == payload.Email, ct);
        }

        if (user is null)
        {
            user = new User
            {
                Email = payload.Email,
                DisplayName = payload.Name ?? payload.Email.Split('@')[0],
                PhotoUrl = payload.Picture,
                AuthProvider = "google",
                ProviderUserId = payload.Sub,
                EmailVerified = false // app-level verification gate
            };
            _db.Users.Add(user);
        }
        else
        {
            // Keep provider info updated
            user.AuthProvider = "google";
            user.ProviderUserId = payload.Sub;
            user.DisplayName = string.IsNullOrWhiteSpace(payload.Name) ? user.DisplayName : payload.Name!;
            user.PhotoUrl = payload.Picture ?? user.PhotoUrl;
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        if (user.IsSuspended)
        {
            return new AuthResponse("", "", RequiresEmailVerification: true, IsSuspended: true, Me(user));
        }

        // Issue tokens
        var access = _tokens.CreateAccessToken(user.Id, user.Email, user.Role, user.EmailVerified);
        var (refresh, refreshHash) = _tokens.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(int.TryParse(_cfg["Jwt:RefreshTokenDays"], out var d) ? d : 30),
            CreatedIp = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = _http.HttpContext?.Request.Headers.UserAgent.ToString()
        });

        await _db.SaveChangesAsync(ct);

        // If not verified, send verification email (app-level)
        if (!user.EmailVerified)
        {
            await SendVerificationEmailInternalAsync(user, ct);
        }

        return new AuthResponse(access, refresh, RequiresEmailVerification: !user.EmailVerified, IsSuspended: false, Me(user));
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest req, CancellationToken ct)
    {
        var hash = _tokens.HashToken(req.RefreshToken);
        var token = await _db.RefreshTokens.Include(x => x.User).SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (token?.User is null || !token.IsActive || token.User.IsSuspended)
            throw new InvalidOperationException("Invalid refresh token.");

        // rotate
        token.RevokedAt = DateTimeOffset.UtcNow;
        var (newRefresh, newHash) = _tokens.CreateRefreshToken();
        token.ReplacedByTokenHash = newHash;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = token.UserId,
            TokenHash = newHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(int.TryParse(_cfg["Jwt:RefreshTokenDays"], out var d) ? d : 30),
            CreatedIp = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = _http.HttpContext?.Request.Headers.UserAgent.ToString()
        });

        var access = _tokens.CreateAccessToken(token.User.Id, token.User.Email, token.User.Role, token.User.EmailVerified);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(access, newRefresh, RequiresEmailVerification: !token.User.EmailVerified, IsSuspended: false, Me(token.User));
    }

    public async Task ResendVerificationAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.SingleAsync(x => x.Id == userId, ct);
        if (user.EmailVerified) return;
        await SendVerificationEmailInternalAsync(user, ct);
    }

    public async Task VerifyEmailAsync(string token, CancellationToken ct)
    {
        var hash = _tokens.HashToken(token);
        var rec = await _db.EmailVerificationTokens.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, ct);

        if (rec?.User is null || !rec.IsValid)
            throw new InvalidOperationException("Verification link is invalid or expired.");

        rec.UsedAt = DateTimeOffset.UtcNow;
        rec.User.EmailVerified = true;

        await _db.SaveChangesAsync(ct);
    }

    private async Task SendVerificationEmailInternalAsync(User user, CancellationToken ct)
    {
        // invalidate previous active tokens (optional)
        var active = await _db.EmailVerificationTokens.Where(x => x.UserId == user.Id && x.UsedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow).ToListAsync(ct);
        foreach (var a in active) a.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var hash = _tokens.HashToken(token);
        var hours = int.TryParse(_cfg["Email:Verification:TokenHours"], out var h) ? h : 24;

        _db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(hours)
        });

        await _db.SaveChangesAsync(ct);

        var webBase = _cfg["Email:Verification:WebBaseUrl"] ?? "http://localhost:5173";
        var link = $"{webBase.TrimEnd('/')}/verify-email?token={Uri.EscapeDataString(token)}";

        var body = $@"
<p>Hi {System.Net.WebUtility.HtmlEncode(user.DisplayName)},</p>
<p>Please verify your email to create or book trips.</p>
<p><a href=""{link}"">Verify Email</a></p>
<p>If you did not request this, you can ignore this email.</p>";

        await _email.SendAsync(user.Email, "Verify your TripShare email", body, ct);
        _log.LogInformation("Verification email queued for {Email}", user.Email);
    }

    private static UserMeDto Me(User u) => new(u.Id, u.Email, u.EmailVerified, u.DisplayName, u.PhotoUrl, u.IsDriver, u.Role);
}
