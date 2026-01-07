using Microsoft.EntityFrameworkCore;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Services;

public sealed class BookingHousekeepingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingHousekeepingService> _log;

    public BookingHousekeepingService(IServiceScopeFactory scopeFactory, ILogger<BookingHousekeepingService> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // run every minute
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Housekeeping run failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch { }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notif = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var now = DateTimeOffset.UtcNow;

        // Expire pending bookings
        var expired = await db.Bookings
            .Include(x => x.Trip)
            .Where(x => x.Status == BookingStatus.Pending && x.PendingExpiresAt != null && x.PendingExpiresAt <= now)
            .OrderBy(x => x.PendingExpiresAt)
            .Take(200)
            .ToListAsync(ct);

        foreach (var b in expired)
        {
            b.Status = BookingStatus.Rejected;
            b.CancellationReason = "Booking request expired";
            b.UpdatedAt = now;

            await notif.CreateAsync(b.PassengerId, NotificationType.BookingRejected, "Booking expired", "Your booking request expired because the driver did not respond in time.", b.TripId, b.Id, ct);
            if (b.Trip != null)
                await notif.CreateAsync(b.Trip.DriverId, NotificationType.System, "Pending booking expired", "A pending booking request has expired.", b.TripId, b.Id, ct);
        }

        // Auto-complete trips/bookings a few hours after departure
        var autoCompleteCutoff = now.AddHours(-6);
        var toComplete = await db.Trips
            .Include(x => x.Bookings)
            .Where(x => (x.Status == TripStatus.Scheduled || x.Status == TripStatus.EnRoute || x.Status == TripStatus.Arrived) && x.DepartureTimeUtc <= autoCompleteCutoff)
            .OrderBy(x => x.DepartureTimeUtc)
            .Take(50)
            .ToListAsync(ct);

        foreach (var t in toComplete)
        {
            t.Status = TripStatus.Completed;
            t.StatusUpdatedAt = now;
            t.UpdatedAt = now;
            t.CompletedAtUtc = now;

            foreach (var b in t.Bookings.Where(x => x.Status == BookingStatus.Accepted))
            {
                b.Status = BookingStatus.Completed;
                b.CompletedAt = now;
                b.UpdatedAt = now;
                b.StatusUpdatedAt = now;
                b.ProgressStatus = BookingProgressStatus.Completed;
                b.ProgressUpdatedAt = now;

                await notif.CreateAsync(b.PassengerId, NotificationType.TripCompleted, "Trip completed", "Trip has been marked completed. You can now leave a rating.", t.Id, b.Id, ct);
            }

            await notif.CreateAsync(t.DriverId, NotificationType.TripCompleted, "Trip completed", "Your trip was auto-completed. You can now receive ratings.", t.Id, null, ct);
        }

        if (expired.Count > 0 || toComplete.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
