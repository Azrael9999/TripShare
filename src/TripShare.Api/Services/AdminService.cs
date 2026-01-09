using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TripShare.Infrastructure.Data;
using TripShare.Domain.Entities;
using TripShare.Api.Controllers;

namespace TripShare.Api.Services;

public sealed class AdminService
{
    private readonly AppDbContext _db;
    private const string DefaultAdminEmail = "admin@tripshare.local";

    public AdminService(AppDbContext db) => _db = db;

    public async Task<object> GetMetricsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var since = now.AddDays(-30);

        var users = await _db.Users.AsNoTracking().CountAsync(ct);
        var activeTrips = await _db.Trips.AsNoTracking().CountAsync(x => x.Status == TripStatus.Scheduled && x.DepartureTimeUtc > now, ct);
        var bookings30d = await _db.Bookings.AsNoTracking().CountAsync(x => x.CreatedAt >= since, ct);
        var reportsOpen = await _db.Reports.AsNoTracking().CountAsync(x => x.Status == ReportStatus.Open, ct);

        return new { users, activeTrips, bookings30d, reportsOpen, nowUtc = now };
    }

    public async Task SuspendUserAsync(Guid userId, bool suspend, string note, CancellationToken ct = default)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (u == null) return;
        if (suspend && string.Equals(u.Email, DefaultAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        u.IsSuspended = suspend;
        u.SuspensionNote = note;
        u.SuspensionUpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task HideTripAsync(Guid tripId, bool hide, string note, CancellationToken ct = default)
    {
        var t = await _db.Trips.FirstOrDefaultAsync(x => x.Id == tripId, ct);
        if (t == null) return;
        t.IsPublic = !hide;
        t.VisibilityNote = note;
        t.VisibilityUpdatedAt = DateTimeOffset.UtcNow;
        t.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<object>> ListAdminsAsync(CancellationToken ct = default)
    {
        return await _db.Users.AsNoTracking()
            .Where(x => x.Role == "admin" || x.Role == "superadmin")
            .OrderByDescending(x => x.Role)
            .ThenBy(x => x.Email)
            .Select(x => new
            {
                x.Id,
                x.Email,
                x.DisplayName,
                x.Role,
                x.AdminApproved,
                x.IsSuspended,
                x.LastLoginAt
            })
            .Cast<object>()
            .ToListAsync(ct);
    }

    public async Task UpsertAdminAsync(AdminUpsertRequest req, CancellationToken ct = default)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Email is required.");

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
                throw new InvalidOperationException("Password must be at least 8 characters.");

            var (hash, salt) = HashPassword(req.Password);
            user = new User
            {
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? email.Split('@')[0] : req.DisplayName!.Trim(),
                AuthProvider = "password",
                ProviderUserId = email,
                PasswordHash = hash,
                PasswordSalt = salt,
                EmailVerified = true,
                PhoneVerified = false,
                Role = "admin",
                AdminApproved = req.Approved,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.Users.Add(user);
        }
        else
        {
            if (user.Role == "superadmin")
                throw new InvalidOperationException("Super admin accounts cannot be edited here.");

            user.Role = "admin";
            user.AdminApproved = req.Approved;
            user.DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? user.DisplayName : req.DisplayName!.Trim();
            user.AuthProvider = "password";
            user.ProviderUserId = email;
            if (!string.IsNullOrWhiteSpace(req.Password))
            {
                if (req.Password.Length < 8)
                    throw new InvalidOperationException("Password must be at least 8 characters.");
                var (hash, salt) = HashPassword(req.Password);
                user.PasswordHash = hash;
                user.PasswordSalt = salt;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<DriverSearchResult>> SearchDriversAsync(string query, CancellationToken ct = default)
    {
        var trimmed = query.Trim();
        var like = $"%{trimmed}%";
        var drivers = _db.Users.AsNoTracking()
            .Include(x => x.Vehicle)
            .Where(x => x.IsDriver || x.Vehicle != null);

        if (Guid.TryParse(trimmed, out var id))
        {
            drivers = drivers.Where(x => x.Id == id);
        }
        else
        {
            drivers = drivers.Where(x =>
                (x.Email != null && EF.Functions.Like(x.Email, like)) ||
                (x.DisplayName != null && EF.Functions.Like(x.DisplayName, like)) ||
                (x.PhoneNumber != null && EF.Functions.Like(x.PhoneNumber, like)) ||
                (x.Vehicle != null && (
                    (x.Vehicle.Make != null && EF.Functions.Like(x.Vehicle.Make, like)) ||
                    (x.Vehicle.Model != null && EF.Functions.Like(x.Vehicle.Model, like)) ||
                    (x.Vehicle.Color != null && EF.Functions.Like(x.Vehicle.Color, like)) ||
                    (x.Vehicle.PlateNumber != null && EF.Functions.Like(x.Vehicle.PlateNumber, like))
                ))
            );
        }

        return await drivers
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.Email)
            .Select(x => new DriverSearchResult(
                x.Id,
                x.DisplayName,
                x.Email,
                x.PhoneNumber,
                x.Vehicle == null
                    ? null
                    : (x.Vehicle.Make ?? "")
                        + (x.Vehicle.Model == null ? "" : $" {x.Vehicle.Model}")
                        + (x.Vehicle.Color == null ? "" : $" {x.Vehicle.Color}")
                        + (x.Vehicle.PlateNumber == null ? "" : $" {x.Vehicle.PlateNumber}")
            ))
            .Take(10)
            .ToListAsync(ct);
    }

    private static (string hash, string salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 12000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }
}
