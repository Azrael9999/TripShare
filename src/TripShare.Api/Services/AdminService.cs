using Microsoft.EntityFrameworkCore;
using TripShare.Infrastructure.Data;
using TripShare.Domain.Entities;

namespace TripShare.Api.Services;

public sealed class AdminService
{
    private readonly AppDbContext _db;

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

    public async Task SuspendUserAsync(Guid userId, bool suspend, CancellationToken ct = default)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (u == null) return;
        u.IsSuspended = suspend;
        await _db.SaveChangesAsync(ct);
    }

    public async Task HideTripAsync(Guid tripId, bool hide, CancellationToken ct = default)
    {
        var t = await _db.Trips.FirstOrDefaultAsync(x => x.Id == tripId, ct);
        if (t == null) return;
        t.IsPublic = !hide;
        if (hide)
        {
            t.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }
}
