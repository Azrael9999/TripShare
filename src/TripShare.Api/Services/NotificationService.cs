using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace TripShare.Api.Services;

public sealed class NotificationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<NotificationService> _log;

    public NotificationService(AppDbContext db, ILogger<NotificationService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task CreateAsync(Guid userId, NotificationType type, string title, string body, Guid? tripId = null, Guid? bookingId = null, CancellationToken ct = default)
    {
        var n = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            TripId = tripId,
            BookingId = bookingId,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Notifications.Add(n);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Notification created type={Type} user={UserId} trip={TripId} booking={BookingId}", type, userId, tripId, bookingId);
    }

    public async Task<List<Notification>> ListAsync(Guid userId, bool unreadOnly, int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var q = _db.Notifications.AsNoTracking().Where(x => x.UserId == userId);
        if (unreadOnly) q = q.Where(x => !x.IsRead);
        return await q.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
    }

    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, ct);
        if (n == null) return;
        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
