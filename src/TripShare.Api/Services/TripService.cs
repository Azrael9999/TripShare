using Microsoft.EntityFrameworkCore;
using TripShare.Application.Contracts;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Services;

public sealed class TripService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TripService> _log;
    private readonly NotificationService _notif;

    public TripService(AppDbContext db, ILogger<TripService> log, NotificationService notif)
    {
        _db = db;
        _log = log;
        _notif = notif;
    }

    public async Task<TripSummaryDto> CreateAsync(Guid driverId, CreateTripRequest req, CancellationToken ct)
    {
        ValidateTripRequest(req);

        var trip = new Trip
        {
            DriverId = driverId,
            DepartureTimeUtc = req.DepartureTimeUtc,
            SeatsTotal = req.SeatsTotal,
            Currency = req.Currency,
            Notes = req.Notes,
            Status = TripStatus.Scheduled,
            InstantBook = req.InstantBook,
            BookingCutoffMinutes = req.BookingCutoffMinutes,
            PendingBookingExpiryMinutes = req.PendingBookingExpiryMinutes,
            IsPublic = true
        };

        // route points
        foreach (var rp in req.RoutePoints.OrderBy(x => x.OrderIndex))
        {
            trip.RoutePoints.Add(new TripRoutePoint
            {
                OrderIndex = rp.OrderIndex,
                Type = rp.Type,
                Lat = rp.Lat,
                Lng = rp.Lng,
                DisplayAddress = rp.DisplayAddress,
                PlaceId = rp.PlaceId
            });
        }

        // segments derived from points
        var orderedPoints = trip.RoutePoints.OrderBy(x => x.OrderIndex).ToList();
        var segmentsCount = orderedPoints.Count - 1;
        if (req.SegmentPrices.Count != segmentsCount)
            throw new InvalidOperationException("SegmentPrices must match number of segments.");

        for (int i = 0; i < segmentsCount; i++)
        {
            var price = req.SegmentPrices.Single(x => x.OrderIndex == i).Price;
            trip.Segments.Add(new TripSegment
            {
                OrderIndex = i,
                FromRoutePointId = orderedPoints[i].Id,
                ToRoutePointId = orderedPoints[i + 1].Id,
                Price = price,
                Currency = trip.Currency,
                BookedSeats = 0
            });
        }

        _db.Trips.Add(trip);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Trip created {TripId} by {DriverId}", trip.Id, driverId);

        return await GetByIdAsync(trip.Id, ct) ?? throw new InvalidOperationException("Trip not found after creation.");
    }

    public async Task<TripSummaryDto?> GetByIdAsync(Guid tripId, CancellationToken ct)
    {
        var trip = await _db.Trips
            .AsNoTracking()
            .Include(x => x.Driver)
            .Include(x => x.RoutePoints)
            .Include(x => x.Segments)
            .SingleOrDefaultAsync(x => x.Id == tripId && x.IsPublic, ct);

        if (trip is null || trip.Driver is null) return null;

        return Map(trip);
    }

    public async Task<PagedResult<TripSummaryDto>> SearchAsync(SearchTripsRequest req, CancellationToken ct)
    {
        var q = _db.Trips
            .AsNoTracking()
            .Include(x => x.Driver)
            .Include(x => x.RoutePoints)
            .Include(x => x.Segments)
            .Where(x => x.IsPublic);


        // Extra filters
        if (req.VerifiedDriversOnly)
            q = q.Where(x => x.Driver != null && x.Driver.EmailVerified && !x.Driver.IsSuspended);

        if (req.MaxPricePerSeat is not null)
        {
            var max = req.MaxPricePerSeat.Value;
            q = q.Where(x => x.Segments.Sum(s => s.Price) <= max);
        }

        if (req.MinDriverRating is not null)
        {
            var min = req.MinDriverRating.Value;
            q = q.Where(x => _db.Ratings.Where(r => r.ToUserId == x.DriverId).Average(r => (decimal?)r.Stars) >= min);
        }


        if (req.FromUtc is not null) q = q.Where(x => x.DepartureTimeUtc >= req.FromUtc.Value);
        if (req.ToUtc is not null) q = q.Where(x => x.DepartureTimeUtc <= req.ToUtc.Value);

        if (!string.IsNullOrWhiteSpace(req.Query))
        {
            var term = req.Query.Trim();
            q = q.Where(x => x.RoutePoints.Any(rp => rp.DisplayAddress.Contains(term)));
        }

        var total = await q.LongCountAsync(ct);
        var page = Math.Max(1, req.Page);
        var size = Math.Clamp(req.PageSize, 1, 50);

        var items = await q
            .OrderBy(x => x.DepartureTimeUtc)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<TripSummaryDto>(items.Select(Map).ToList(), page, size, total);
    }

    public async Task<PagedResult<TripSummaryDto>> MyTripsAsync(Guid driverId, int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 50 ? 20 : pageSize;

        var q = _db.Trips
            .AsNoTracking()
            .Include(x => x.Driver)
            .Include(x => x.RoutePoints)
            .Include(x => x.Segments)
            .Where(x => x.DriverId == driverId)
            .OrderByDescending(x => x.DepartureTimeUtc);

        var total = await q.LongCountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<TripSummaryDto>(items.Select(Map).ToList(), page, pageSize, total);
    }


    private static TripSummaryDto Map(Trip t)
        => new(
            t.Id,
            t.DriverId,
            t.Driver?.DisplayName ?? "Driver",
            t.Driver?.PhotoUrl,
            t.DepartureTimeUtc,
            t.SeatsTotal,
            t.Currency,
            t.RoutePoints.OrderBy(x => x.OrderIndex).Select(x => new RoutePointDto(x.Id, x.OrderIndex, x.Type, x.Lat, x.Lng, x.DisplayAddress)).ToList(),
            t.Segments.OrderBy(x => x.OrderIndex).Select(x => new SegmentDto(x.Id, x.OrderIndex, x.FromRoutePointId, x.ToRoutePointId, x.Price, x.Currency)).ToList()
        );

    private static void ValidateTripRequest(CreateTripRequest req)
    {
        if (req.SeatsTotal < 1 || req.SeatsTotal > 8) throw new InvalidOperationException("SeatsTotal must be 1..8.");
        if (string.IsNullOrWhiteSpace(req.Currency)) throw new InvalidOperationException("Currency required.");
        if (req.RoutePoints is null || req.RoutePoints.Count < 2) throw new InvalidOperationException("At least start and end are required.");

        var ordered = req.RoutePoints.OrderBy(x => x.OrderIndex).ToList();
        if (ordered.First().Type != RoutePointType.Start) throw new InvalidOperationException("First route point must be Start.");
        if (ordered.Last().Type != RoutePointType.End) throw new InvalidOperationException("Last route point must be End.");

        // indices must be 0..n-1
        for (int i = 0; i < ordered.Count; i++)
            if (ordered[i].OrderIndex != i) throw new InvalidOperationException("Route point OrderIndex must be continuous starting at 0.");

        // stop cap
        if (ordered.Count > 12) throw new InvalidOperationException("Max 10 stops (12 points including start/end).");
    }


    public async Task StartTripAsync(Guid driverId, Guid tripId, CancellationToken ct)
    {
        var trip = await _db.Trips.Include(x => x.Bookings).FirstOrDefaultAsync(x => x.Id == tripId, ct);
        if (trip == null) throw new InvalidOperationException("Trip not found.");
        if (trip.DriverId != driverId) throw new UnauthorizedAccessException();
        if (trip.Status != TripStatus.Scheduled) throw new InvalidOperationException("Trip cannot be started in current state.");

        trip.Status = TripStatus.InProgress;
        trip.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        foreach (var b in trip.Bookings.Where(x => x.Status == BookingStatus.Accepted))
            await _notif.CreateAsync(b.PassengerId, NotificationType.TripStarted, "Trip started", "Your driver has started the trip.", trip.Id, b.Id, ct);

        await _notif.CreateAsync(driverId, NotificationType.TripStarted, "Trip started", "You started the trip.", trip.Id, null, ct);
    }

    public async Task CompleteTripAsync(Guid driverId, Guid tripId, CancellationToken ct)
    {
        var trip = await _db.Trips.Include(x => x.Bookings).FirstOrDefaultAsync(x => x.Id == tripId, ct);
        if (trip == null) throw new InvalidOperationException("Trip not found.");
        if (trip.DriverId != driverId) throw new UnauthorizedAccessException();
        if (trip.Status is TripStatus.Cancelled or TripStatus.Completed) throw new InvalidOperationException("Trip is already finished.");

        trip.Status = TripStatus.Completed;
        trip.UpdatedAt = DateTimeOffset.UtcNow;

        foreach (var b in trip.Bookings.Where(x => x.Status == BookingStatus.Accepted))
        {
            b.Status = BookingStatus.Completed;
            b.CompletedAt = DateTimeOffset.UtcNow;
            b.UpdatedAt = DateTimeOffset.UtcNow;
            await _notif.CreateAsync(b.PassengerId, NotificationType.TripCompleted, "Trip completed", "Trip completed. You can leave a rating now.", trip.Id, b.Id, ct);
        }

        await _db.SaveChangesAsync(ct);
        await _notif.CreateAsync(driverId, NotificationType.TripCompleted, "Trip completed", "Trip completed. You can receive ratings now.", trip.Id, null, ct);
    }

    public async Task CancelTripAsync(Guid driverId, Guid tripId, string? reason, CancellationToken ct)
    {
        var trip = await _db.Trips.Include(x => x.Bookings).FirstOrDefaultAsync(x => x.Id == tripId, ct);
        if (trip == null) throw new InvalidOperationException("Trip not found.");
        if (trip.DriverId != driverId) throw new UnauthorizedAccessException();
        if (trip.Status == TripStatus.Cancelled) return;

        trip.Status = TripStatus.Cancelled;
        trip.IsPublic = false;
        trip.Notes = string.IsNullOrWhiteSpace(reason) ? trip.Notes : $"{trip.Notes}\nCancelled: {reason}";
        trip.UpdatedAt = DateTimeOffset.UtcNow;

        foreach (var b in trip.Bookings.Where(x => x.Status is BookingStatus.Pending or BookingStatus.Accepted))
        {
            b.Status = BookingStatus.Cancelled;
            b.CancellationReason = "Trip cancelled by driver";
            b.UpdatedAt = DateTimeOffset.UtcNow;
            await _notif.CreateAsync(b.PassengerId, NotificationType.TripCancelled, "Trip cancelled", "Trip was cancelled by the driver.", trip.Id, b.Id, ct);
        }

        await _db.SaveChangesAsync(ct);
        await _notif.CreateAsync(driverId, NotificationType.TripCancelled, "Trip cancelled", "Trip cancelled.", trip.Id, null, ct);
    }

    public async Task SetVisibilityAsync(Guid driverId, Guid tripId, bool isPublic, CancellationToken ct)
    {
        var trip = await _db.Trips.FirstOrDefaultAsync(x => x.Id == tripId, ct);
        if (trip == null) throw new InvalidOperationException("Trip not found.");
        if (trip.DriverId != driverId) throw new UnauthorizedAccessException();
        trip.IsPublic = isPublic;
        trip.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

}
