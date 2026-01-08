using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TripShare.Application.Abstractions;
using TripShare.Application.Contracts;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Services;

public sealed class AuthService
{
    private const string DefaultAdminEmail = "admin@tripshare.local";
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IGoogleIdTokenValidator _google;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AuthService> _log;
    private readonly IHttpContextAccessor _http;

    public AuthService(
        AppDbContext db,
        ITokenService tokens,
        IGoogleIdTokenValidator google,
        IEmailSender email,
        ISmsSender sms,
        IConfiguration cfg,
        ILogger<AuthService> log,
        IHttpContextAccessor http)
    {
        _db = db;
        _tokens = tokens;
        _google = google;
        _email = email;
        _sms = sms;
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
        EnsureActive(user);

        // Issue tokens
        var access = _tokens.CreateAccessToken(user.Id, user.Email, user.Role, user.EmailVerified, user.PhoneVerified);
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

    public async Task<AuthResponse> PasswordRegisterAsync(PasswordRegisterRequest req, CancellationToken ct)
    {
        var email = NormalizeEmail(req.Email);
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
            throw new InvalidOperationException("Password must be at least 8 characters.");

        var existing = await _db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        if (existing is not null)
            throw new InvalidOperationException("An account already exists for this email.");

        var (hash, salt) = HashPassword(req.Password);
        var user = new User
        {
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? email.Split('@')[0] : req.DisplayName,
            AuthProvider = "password",
            ProviderUserId = email,
            PasswordHash = hash,
            PasswordSalt = salt,
            EmailVerified = false,
            PhoneVerified = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        await SendVerificationEmailInternalAsync(user, ct);
        return await PasswordLoginAsync(new PasswordLoginRequest(email, req.Password, req.Timezone, req.Locale), ct);
    }

    public async Task<AuthResponse> PasswordLoginAsync(PasswordLoginRequest req, CancellationToken ct)
    {
        var email = NormalizeEmail(req.Email);
        var user = await _db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash) || string.IsNullOrWhiteSpace(user.PasswordSalt))
            throw new InvalidOperationException("Invalid credentials.");

        if (!VerifyPassword(req.Password, user.PasswordHash, user.PasswordSalt))
            throw new InvalidOperationException("Invalid credentials.");

        if (user.IsSuspended)
            return new AuthResponse("", "", RequiresEmailVerification: true, IsSuspended: true, Me(user));
        EnsureActive(user);

        user.AuthProvider = "password";
        user.ProviderUserId = email;
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var access = _tokens.CreateAccessToken(user.Id, user.Email, user.Role, user.EmailVerified, user.PhoneVerified);
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

        if (!user.EmailVerified)
            await SendVerificationEmailInternalAsync(user, ct);

        return new AuthResponse(access, refresh, RequiresEmailVerification: !user.EmailVerified, IsSuspended: false, Me(user));
    }

    public async Task RequestPasswordResetAsync(PasswordResetRequest req, CancellationToken ct)
    {
        var email = NormalizeEmail(req.Email);
        if (string.IsNullOrWhiteSpace(email))
            return;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash) || string.IsNullOrWhiteSpace(user.PasswordSalt))
        {
            return;
        }

        EnsureActive(user);

        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var hash = _tokens.HashToken(token);
        var hours = int.TryParse(_cfg["Email:PasswordReset:TokenHours"], out var h) ? h : 2;

        _db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(hours)
        });

        await _db.SaveChangesAsync(ct);

        var webBase = _cfg["Email:PasswordReset:WebBaseUrl"] ?? _cfg["Email:Verification:WebBaseUrl"] ?? "http://localhost:5173";
        var link = $"{webBase.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";

        var body = $@"
<p>Hi {System.Net.WebUtility.HtmlEncode(user.DisplayName)},</p>
<p>We received a request to reset your HopTrip password.</p>
<p><a href=""{link}"">Reset password</a></p>
<p>This link expires in {hours} hour(s). If you did not request this, you can ignore this email.</p>";

        await _email.SendAsync(user.Email, "Reset your HopTrip password", body, ct);
        _log.LogInformation("Password reset email queued for {Email}", user.Email);
    }

    public async Task ConfirmPasswordResetAsync(PasswordResetConfirm req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Token))
            throw new InvalidOperationException("Reset token is required.");
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            throw new InvalidOperationException("Password must be at least 8 characters.");

        var hash = _tokens.HashToken(req.Token);
        var reset = await _db.PasswordResetTokens.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, ct);

        if (reset?.User is null || !reset.IsValid)
            throw new InvalidOperationException("Reset link is invalid or expired.");

        var user = await _db.Users.Include(x => x.RefreshTokens)
            .SingleAsync(x => x.Id == reset.User.Id, ct);
        EnsureActive(user);

        var (newHash, newSalt) = HashPassword(req.NewPassword);
        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;
        user.AuthProvider = "password";
        user.ProviderUserId = user.Email;
        reset.UsedAt = DateTimeOffset.UtcNow;

        foreach (var token in user.RefreshTokens.Where(x => x.RevokedAt == null))
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest req, CancellationToken ct)
    {
        var hash = _tokens.HashToken(req.RefreshToken);
        var token = await _db.RefreshTokens.Include(x => x.User).SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (token?.User is null || !token.IsActive || token.User.IsSuspended)
            throw new InvalidOperationException("Invalid refresh token.");
        EnsureActive(token.User);

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

        var access = _tokens.CreateAccessToken(token.User.Id, token.User.Email, token.User.Role, token.User.EmailVerified, token.User.PhoneVerified);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(access, newRefresh, RequiresEmailVerification: !token.User.EmailVerified, IsSuspended: false, Me(token.User));
    }

    public async Task ResendVerificationAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.SingleAsync(x => x.Id == userId, ct);
        EnsureActive(user);
        if (user.IsSuspended)
            throw new InvalidOperationException("Account is suspended.");
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
        EnsureActive(rec.User);

        await _db.SaveChangesAsync(ct);
    }

    public async Task RequestSmsOtpAsync(SmsOtpRequest req, CancellationToken ct)
    {
        var phone = NormalizePhone(req.PhoneNumber);
        if (string.IsNullOrWhiteSpace(phone))
            throw new InvalidOperationException("Phone number is required.");

        var expiryMinutes = int.TryParse(_cfg["Sms:OtpMinutes"], out var m) ? m : 10;

        var user = await _db.Users.SingleOrDefaultAsync(x => x.PhoneNumber == phone, ct);
        if (user is null)
        {
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            var aliasEmail = $"sms{digits}@hoptrip-sms.local";
            var display = $"User {TrimForDisplay(phone)}";
            user = new User
            {
                Email = aliasEmail,
                DisplayName = display,
                PhoneNumber = phone,
                AuthProvider = "sms",
                ProviderUserId = phone,
                EmailVerified = false
            };
            _db.Users.Add(user);
        }
        else if (user.IsSuspended)
        {
            throw new InvalidOperationException("Account is suspended.");
        }
        else
        {
            EnsureActive(user);
        }

        var active = await _db.SmsOtpTokens
            .Where(x => x.UserId == user.Id && x.UsedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(ct);
        foreach (var a in active) a.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        var otp = Random.Shared.Next(100000, 999999).ToString();
        var hash = _tokens.HashToken(otp);
        _db.SmsOtpTokens.Add(new SmsOtpToken
        {
            UserId = user.Id,
            PhoneNumber = phone,
            TokenHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes)
        });

        await _db.SaveChangesAsync(ct);

        var msg = $"Your HopTrip code is {otp}. It expires in {expiryMinutes} minutes.";
        await _sms.SendAsync(phone, msg, ct);
        _log.LogInformation("SMS OTP sent to {Phone}", phone);
    }

    public async Task RequestPhoneVerificationAsync(Guid userId, string phoneNumber, CancellationToken ct)
    {
        var phone = NormalizePhone(phoneNumber);
        if (string.IsNullOrWhiteSpace(phone))
            throw new InvalidOperationException("Phone number is required.");

        var user = await _db.Users.SingleAsync(x => x.Id == userId, ct);
        EnsureActive(user);

        var inUse = await _db.Users.AsNoTracking()
            .AnyAsync(x => x.Id != userId && x.PhoneNumber == phone && x.PhoneVerified, ct);
        if (inUse)
            throw new InvalidOperationException("Phone number is already verified on another account.");

        var expiryMinutes = int.TryParse(_cfg["Sms:OtpMinutes"], out var m) ? m : 10;

        var active = await _db.SmsOtpTokens
            .Where(x => x.UserId == user.Id && x.UsedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(ct);
        foreach (var a in active) a.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        var otp = Random.Shared.Next(100000, 999999).ToString();
        var hash = _tokens.HashToken(otp);
        _db.SmsOtpTokens.Add(new SmsOtpToken
        {
            UserId = user.Id,
            PhoneNumber = phone,
            TokenHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes)
        });

        await _db.SaveChangesAsync(ct);

        var msg = $"Your HopTrip phone verification code is {otp}. It expires in {expiryMinutes} minutes.";
        await _sms.SendAsync(phone, msg, ct);
        _log.LogInformation("Phone verification OTP sent to {Phone}", phone);
    }

    public async Task VerifyPhoneVerificationAsync(Guid userId, string phoneNumber, string otp, CancellationToken ct)
    {
        var phone = NormalizePhone(phoneNumber);
        var trimmedOtp = otp?.Trim();
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(trimmedOtp))
            throw new InvalidOperationException("Phone and OTP are required.");

        var hash = _tokens.HashToken(trimmedOtp);
        var token = await _db.SmsOtpTokens
            .Include(x => x.User)
            .Where(x => x.UserId == userId && x.PhoneNumber == phone && x.TokenHash == hash && x.UsedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (token?.User is null || !token.IsValid)
            throw new InvalidOperationException("Invalid or expired code.");

        token.UsedAt = DateTimeOffset.UtcNow;

        var user = token.User;
        EnsureActive(user);
        if (user.IsSuspended)
            throw new InvalidOperationException("Account is suspended.");
        user.PhoneNumber = phone;
        user.PhoneVerified = true;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<AuthResponse> VerifySmsOtpAsync(SmsOtpVerifyRequest req, CancellationToken ct)
    {
        var phone = NormalizePhone(req.PhoneNumber);
        var otp = req.Otp?.Trim();
        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(otp))
            throw new InvalidOperationException("Phone and OTP are required.");

        var hash = _tokens.HashToken(otp);
        var token = await _db.SmsOtpTokens
            .Include(x => x.User)
            .Where(x => x.PhoneNumber == phone && x.TokenHash == hash && x.UsedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (token?.User is null || !token.IsValid)
            throw new InvalidOperationException("Invalid or expired code.");

        token.UsedAt = DateTimeOffset.UtcNow;

        var user = token.User;
        if (user.IsSuspended)
            throw new InvalidOperationException("Account is suspended.");
        EnsureActive(user);
        user.PhoneNumber ??= phone;
        user.PhoneVerified = true;
        user.EmailVerified = true; // treat verified phone as verified gate
        user.LastLoginAt = DateTimeOffset.UtcNow;

        var access = _tokens.CreateAccessToken(user.Id, user.Email, user.Role, user.EmailVerified, user.PhoneVerified);
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

        return new AuthResponse(access, refresh, RequiresEmailVerification: false, IsSuspended: user.IsSuspended, Me(user));
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

        await _email.SendAsync(user.Email, "Verify your HopTrip email", body, ct);
        _log.LogInformation("Verification email queued for {Email}", user.Email);
    }

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest req, CancellationToken ct)
    {
        var user = await _db.Users.SingleAsync(x => x.Id == userId, ct);
        EnsureActive(user);

        if (!string.IsNullOrWhiteSpace(req.DisplayName))
            user.DisplayName = req.DisplayName.Trim();

        user.PhotoUrl = string.IsNullOrWhiteSpace(req.PhotoUrl) ? null : req.PhotoUrl.Trim();
        if (!string.IsNullOrWhiteSpace(req.PhoneNumber))
            user.PhoneNumber = NormalizePhone(req.PhoneNumber);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<AccountExportDto> ExportAccountAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().SingleAsync(x => x.Id == userId, ct);
        EnsureActive(user);

        var trips = await _db.Trips.AsNoTracking()
            .Where(x => x.DriverId == userId)
            .Select(x => new { x.Id, x.Status, x.DepartureTimeUtc, x.Currency, x.Notes })
            .ToListAsync(ct);

        var bookings = await _db.Bookings.AsNoTracking()
            .Where(x => x.PassengerId == userId)
            .Select(x => new { x.Id, x.TripId, x.Status, x.CreatedAt, x.Currency })
            .ToListAsync(ct);

        var ratings = await _db.Ratings.AsNoTracking()
            .Where(x => x.FromUserId == userId || x.ToUserId == userId)
            .Select(x => new { x.Id, x.BookingId, x.FromUserId, x.ToUserId, x.Stars, x.Comment, x.CreatedAt })
            .ToListAsync(ct);

        var reports = await _db.Reports.AsNoTracking()
            .Where(x => x.ReporterUserId == userId || x.TargetUserId == userId)
            .Select(x => new { x.Id, x.TargetType, x.TargetUserId, x.TripId, x.BookingId, x.Reason, x.Status, x.CreatedAt })
            .ToListAsync(ct);

        var messages = await (from m in _db.Messages.AsNoTracking()
                              join t in _db.MessageThreads.AsNoTracking() on m.ThreadId equals t.Id
                              where t.ParticipantAId == userId || t.ParticipantBId == userId || m.SenderId == userId
                              select new { m.Id, m.ThreadId, m.Body, m.SentAt, m.IsSystem })
            .ToListAsync(ct);

        return new AccountExportDto(
            user.Email,
            user.DisplayName,
            user.EmailVerified,
            user.PhoneVerified,
            user.IdentityVerified,
            trips.Cast<object>().ToList(),
            bookings.Cast<object>().ToList(),
            ratings.Cast<object>().ToList(),
            reports.Cast<object>().ToList(),
            messages.Cast<object>().ToList());
    }

    public async Task DeleteAccountAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users.Include(x => x.RefreshTokens).FirstAsync(x => x.Id == userId, ct);
        EnsureActive(user);
        if (string.Equals(user.Email, DefaultAdminEmail, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The default admin account cannot be deleted.");
        user.IsDeleted = true;
        user.DeletedAt = DateTimeOffset.UtcNow;
        foreach (var token in user.RefreshTokens.Where(x => x.RevokedAt == null))
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    private static UserMeDto Me(User u) => new(u.Id, u.Email, u.EmailVerified, u.DisplayName, u.PhotoUrl, u.IsDriver, u.DriverVerified, u.IdentityVerified, u.PhoneVerified, u.Role);

    private static string NormalizePhone(string phone)
    {
        var trimmed = phone.Trim();
        if (trimmed.StartsWith("00")) trimmed = "+" + trimmed[2..];
        return trimmed.Replace(" ", "").Replace("-", "");
    }

    private static string TrimForDisplay(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? digits[^4..] : digits;
    }

    private static (string hash, string salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 12000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    private static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedSalt)) return false;
        var saltBytes = Convert.FromBase64String(storedSalt);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 12000, HashAlgorithmName.SHA256, 32);
        try
        {
            return CryptographicOperations.FixedTimeEquals(hashBytes, Convert.FromBase64String(storedHash));
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static void EnsureActive(User user)
    {
        if (user.IsDeleted)
            throw new InvalidOperationException("This account has been deleted.");
    }
}
