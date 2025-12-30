using Microsoft.EntityFrameworkCore;
using TripShare.Api.Helpers;
using TripShare.Application.Contracts;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace TripShare.Api.Services;

public sealed class TripService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TripService> _log;
    private readonly NotificationService _notif;
    private readonly SiteSettingsService _settings;
    private readonly IMemoryCache _cache;
    private readonly TripLocationStreamService _locationStream;

    private const double DefaultAverageSpeedKmh = 45d;
    private static readonly TimeSpan MinLocationUpdateInterval = TimeSpan.FromSeconds(3);

    public TripService(
        AppDbContext db,
        ILogger<TripService> log,
        NotificationService notif,
        SiteSettingsService settings,
        IMemoryCache cache,
        TripLocationStreamService locationStream)
    {
        _db = db;
        _log = log;
        _notif = notif;
        _settings = settings;
        _cache = cache;
        _locationStream = locationStream;
    }

    public async Task<TripSummaryDto> CreateAsync(Guid driverId, CreateTripRequest req, CancellationToken ct)
    {
        ValidateTripRequest(req);

        // Enforce driver verification if enabled
        if (await _settings.GetDriverVerificationRequiredAsync(ct))
        {
            var driver = await _db.Users.AsNoTracking().SingleAsync(x => x.Id == driverId, ct);
            if (!driver.DriverVerified)
                throw new InvalidOperationException("Driver verification is required before creating trips.");
        }

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

    public Task<TripSummaryDto?> GetByIdAsync(Guid tripId, CancellationToken ct)
        => GetByIdAsync(tripId, null, ct);

    public async Task<TripSummaryDto?> GetByIdAsync(Guid tripId, Guid? requesterId, CancellationToken ct)
    {
        var trip = await _db.Trips
            .AsNoTracking()
            .Include(x => x.Driver)
            .Include(x => x.RoutePoints)
            .Include(x => x.Segments)
            .Include(x => x.Bookings)
            .SingleOrDefaultAsync(x => x.Id == tripId, ct);

        if (trip is null || trip.Driver is null) return null;

        if (!trip.IsPublic && requesterId is null)
            return null;

        if (!trip.IsPublic && requesterId is not null && trip.DriverId != requesterId && !trip.Bookings.Any(b => b.PassengerId == requesterId))
            throw new UnauthorizedAccessException();

        return Map(trip);
    }

    public async Task<PagedResult<TripSummaryDto>> SearchAsync(SearchTripsRequest req, CancellationToken ct)
    {
        var cacheKey = $"search:{req.Query}:{req.FromUtc}:{req.ToUtc}:{req.MaxPricePerSeat}:{req.MinDriverRating}:{req.VerifiedDriversOnly}:{req.Page}:{req.PageSize}";
        if (_cache.TryGetValue<PagedResult<TripSummaryDto>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

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

        var result = new PagedResult<TripSummaryDto>(items.Select(Map).ToList(), page, size, total);
        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(30));
        return result;
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
            t.Status,
            t.StatusUpdatedAt,
            t.StartedAtUtc,
            t.ArrivedAtUtc,
            t.CompletedAtUtc,
            t.CancelledAtUtc,
            t.CurrentLat,
            t.CurrentLng,
            t.CurrentHeading,
            t.LocationUpdatedAt,
            t.RoutePoints.OrderBy(x => x.OrderIndex).Select(x => new RoutePointDto(x.Id, x.OrderIndex, x.Type, x.Lat, x.Lng, x.DisplayAddress)).ToList(),
            t.Segments.OrderBy(x => x.OrderIndex).Select(x => new SegmentDto(x.Id, x.OrderIndex, x.FromRoutePointId, x.ToRoutePointId, x.Price, x.Currency)).ToList(),
            t.Notes
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


    public Task StartTripAsync(Guid driverId, Guid tripId, CancellationToken ct)
        => ApplyStatusAsync(driverId, tripId, TripStatus.InProgress, null, ct);

    public Task CompleteTripAsync(Guid driverId, Guid tripId, CancellationToken ct)
        => ApplyStatusAsync(driverId, tripId, TripStatus.Completed, null, ct);

    public Task CancelTripAsync(Guid driverId, Guid tripId, string? reason, CancellationToken ct)
        => ApplyStatusAsync(driverId, tripId, TripStatus.Cancelled, reason, ct);

    public Task SetStatusAsync(Guid driverId, Guid tripId, UpdateTripStatusRequest req, CancellationToken ct)
        => ApplyStatusAsync(driverId, tripId, ParseStatus(req.Status), req.Reason, ct);

    public async Task UpdateLocationAsync(Guid driverId, Guid tripId, UpdateTripLocationRequest req, CancellationToken ct)
    {
        if (!GeoHelper.IsValidCoordinate(req.Lat, req.Lng))
            throw new InvalidOperationException("Invalid coordinates.");
        if (req.Heading is < 0 or > 360)
            throw new InvalidOperationException("Heading must be between 0-360 degrees.");

        var trip = await _db.Trips.FirstOrDefaultAsync(x => x.Id == tripId, ct);
        if (trip == null) throw new InvalidOperationException("Trip not found.");
        if (trip.DriverId != driverId) throw new UnauthorizedAccessException();
        if (trip.Status is TripStatus.Completed or TripStatus.Cancelled)
            throw new InvalidOperationException("Trip is finished.");

        var now = DateTimeOffset.UtcNow;
        if (trip.LocationUpdatedAt is not null && now - trip.LocationUpdatedAt < MinLocationUpdateInterval)
            throw new InvalidOperationException("Too many updates. Please slow down.");

        trip.CurrentLat = req.Lat;
        trip.CurrentLng = req.Lng;
        trip.CurrentHeading = req.Heading;
        trip.LocationUpdatedAt = now;
        trip.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        await _locationStream.PublishAsync(
            trip.Id,
            new TripLocationUpdateDto(
                trip.Id,
                trip.CurrentLat!.Value,
                trip.CurrentLng!.Value,
                trip.CurrentHeading,
                trip.LocationUpdatedAt!.Value),
            ct);
    }

    public async Task<TripEtaResponse> CalculateEtasAsync(Guid actorId, Guid tripId, CancellationToken ct)
    {
        var trip = await _db.Trips
            .AsNoTracking()
            .Include(x => x.RoutePoints)
            .Include(x => x.Bookings)
            .SingleOrDefaultAsync(x => x.Id == tripId, ct);

        if (trip is null) throw new InvalidOperationException("Trip not found.");

        var canSee = trip.DriverId == actorId || trip.Bookings.Any(b => b.PassengerId == actorId);
        if (!canSee) throw new UnauthorizedAccessException();

        var orderedPoints = trip.RoutePoints.OrderBy(x => x.OrderIndex).ToList();
        if (orderedPoints.Count < 2) throw new InvalidOperationException("Route missing.");

        var originLat = trip.CurrentLat ?? orderedPoints[0].Lat;
        var originLng = trip.CurrentLng ?? orderedPoints[0].Lng;

        var results = new List<EtaResultDto>();
        var now = DateTimeOffset.UtcNow;

        foreach (var b in trip.Bookings.Where(x => x.Status == BookingStatus.Accepted))
        {
            var pickup = orderedPoints.SingleOrDefault(x => x.Id == b.PickupRoutePointId);
            var drop = orderedPoints.SingleOrDefault(x => x.Id == b.DropoffRoutePointId);
            if (pickup is null || drop is null || drop.OrderIndex <= pickup.OrderIndex)
                continue;

            var pickupLat = GeoHelper.IsValidCoordinate(b.PickupLat, b.PickupLng) ? b.PickupLat : pickup.Lat;
            var pickupLng = GeoHelper.IsValidCoordinate(b.PickupLat, b.PickupLng) ? b.PickupLng : pickup.Lng;
            var dropLat = GeoHelper.IsValidCoordinate(b.DropoffLat, b.DropoffLng) ? b.DropoffLat : drop.Lat;
            var dropLng = GeoHelper.IsValidCoordinate(b.DropoffLat, b.DropoffLng) ? b.DropoffLng : drop.Lng;

            var toPickupKm = GeoHelper.DistanceInKm(originLat, originLng, pickupLat, pickupLng);
            var alongRouteKm = SumRouteDistanceKm(orderedPoints, pickup.OrderIndex, drop.OrderIndex);
            var toDropKm = GeoHelper.DistanceInKm(pickupLat, pickupLng, dropLat, dropLng);

            var etaPickupSeconds = (int)Math.Round((toPickupKm / DefaultAverageSpeedKmh) * 3600);
            var etaDropoffSeconds = (int)Math.Round(((toPickupKm + alongRouteKm + toDropKm) / DefaultAverageSpeedKmh) * 3600);

            results.Add(new EtaResultDto(b.Id, etaPickupSeconds, etaDropoffSeconds, now));
        }

        return new TripEtaResponse(trip.Id, results);
    }

    public async IAsyncEnumerable<TripLocationUpdateDto> StreamLocationsAsync(Guid actorId, Guid tripId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var trip = await _db.Trips
            .AsNoTracking()
            .Include(x => x.Bookings)
            .SingleOrDefaultAsync(x => x.Id == tripId, ct);

        if (trip is null) throw new InvalidOperationException("Trip not found.");

        var canSee = trip.DriverId == actorId || trip.Bookings.Any(b => b.PassengerId == actorId);
        if (!canSee) throw new UnauthorizedAccessException();

        var subscription = _locationStream.Subscribe(tripId, ct);
        if (trip.CurrentLat is not null && trip.CurrentLng is not null && trip.LocationUpdatedAt is not null)
        {
            yield return new TripLocationUpdateDto(trip.Id, trip.CurrentLat.Value, trip.CurrentLng.Value, trip.CurrentHeading, trip.LocationUpdatedAt.Value);
        }

        if (subscription.Latest is not null)
        {
            yield return subscription.Latest;
        }

        await foreach (var update in subscription.Stream.WithCancellation(ct))
        {
            yield return update;
        }
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

    private static TripStatus ParseStatus(string status)
        => status.Trim().ToLowerInvariant() switch
        {
            "scheduled" => TripStatus.Scheduled,
            "enroute" or "en-route" => TripStatus.EnRoute,
            "arrived" => TripStatus.Arrived,
            "inprogress" or "in-progress" => TripStatus.InProgress,
            "completed" => TripStatus.Completed,
            "cancelled" => TripStatus.Cancelled,
            _ => throw new InvalidOperationException("Invalid status.")
        };

    private static bool IsTransitionAllowed(TripStatus current, TripStatus next)
        => current switch
        {
            TripStatus.Scheduled => next is TripStatus.EnRoute or TripStatus.InProgress or TripStatus.Cancelled,
            TripStatus.EnRoute => next is TripStatus.Arrived or TripStatus.Cancelled,
            TripStatus.Arrived => next is TripStatus.InProgress or TripStatus.Cancelled,
            TripStatus.InProgress => next is TripStatus.Completed or TripStatus.Cancelled,
            _ => false
        };

    private async Task ApplyStatusAsync(Guid driverId, Guid tripId, TripStatus target, string? reason, CancellationToken ct)
    {
        var trip = await _db.Trips
            .Include(x => x.Bookings)
            .FirstOrDefaultAsync(x => x.Id == tripId, ct);

        if (trip == null) throw new InvalidOperationException("Trip not found.");
        if (trip.DriverId != driverId) throw new UnauthorizedAccessException();

        if (trip.Status == target) return;
        if (!IsTransitionAllowed(trip.Status, target))
            throw new InvalidOperationException("Trip cannot transition to the requested status.");

        var now = DateTimeOffset.UtcNow;
        trip.Status = target;
        trip.StatusUpdatedAt = now;
        trip.UpdatedAt = now;

        switch (target)
        {
            case TripStatus.EnRoute:
                trip.StartedAtUtc ??= now;
                UpdateBookingProgress(trip.Bookings, BookingProgressStatus.DriverEnRoute, now);
                await NotifyPassengers(trip, NotificationType.TripUpdated, "Driver en route", "Your driver is on the way.", ct);
                await _notif.CreateAsync(driverId, NotificationType.TripUpdated, "Trip en route", "You marked the trip as en route.", trip.Id, null, ct);
                break;
            case TripStatus.Arrived:
                trip.ArrivedAtUtc ??= now;
                UpdateBookingProgress(trip.Bookings, BookingProgressStatus.DriverArrived, now);
                await NotifyPassengers(trip, NotificationType.TripUpdated, "Driver arrived", "Your driver is at the pickup.", ct);
                await _notif.CreateAsync(driverId, NotificationType.TripUpdated, "Arrived", "You marked arrival at pickup.", trip.Id, null, ct);
                break;
            case TripStatus.InProgress:
                trip.StartedAtUtc ??= now;
                UpdateBookingProgress(trip.Bookings, BookingProgressStatus.Riding, now);
                await NotifyPassengers(trip, NotificationType.TripStarted, "Trip started", "Your driver has started the trip.", ct);
                await _notif.CreateAsync(driverId, NotificationType.TripStarted, "Trip started", "You started the trip.", trip.Id, null, ct);
                break;
            case TripStatus.Completed:
                trip.CompletedAtUtc ??= now;
                UpdateBookingProgress(trip.Bookings, BookingProgressStatus.Completed, now);
                foreach (var b in trip.Bookings.Where(x => x.Status == BookingStatus.Accepted))
                {
                    b.Status = BookingStatus.Completed;
                    b.CompletedAt = now;
                    b.UpdatedAt = now;
                    b.StatusUpdatedAt = now;
                    await _notif.CreateAsync(b.PassengerId, NotificationType.TripCompleted, "Trip completed", "Trip completed. You can leave a rating now.", trip.Id, b.Id, ct);
                }
                await _notif.CreateAsync(driverId, NotificationType.TripCompleted, "Trip completed", "Trip completed. You can receive ratings now.", trip.Id, null, ct);
                break;
            case TripStatus.Cancelled:
                trip.CancelledAtUtc ??= now;
                trip.IsPublic = false;
                trip.Notes = string.IsNullOrWhiteSpace(reason) ? trip.Notes : $"{trip.Notes}\nCancelled: {reason}";
                UpdateBookingProgress(trip.Bookings, BookingProgressStatus.Cancelled, now);
                foreach (var b in trip.Bookings.Where(x => x.Status is BookingStatus.Pending or BookingStatus.Accepted))
                {
                    b.Status = BookingStatus.Cancelled;
                    b.CancellationReason = "Trip cancelled by driver";
                    b.UpdatedAt = now;
                    b.StatusUpdatedAt = now;
                    await _notif.CreateAsync(b.PassengerId, NotificationType.TripCancelled, "Trip cancelled", "Trip was cancelled by the driver.", trip.Id, b.Id, ct);
                }
                await _notif.CreateAsync(driverId, NotificationType.TripCancelled, "Trip cancelled", "Trip cancelled.", trip.Id, null, ct);
                break;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task NotifyPassengers(Trip trip, NotificationType type, string title, string body, CancellationToken ct)
    {
        foreach (var b in trip.Bookings.Where(x => x.Status == BookingStatus.Accepted))
            await _notif.CreateAsync(b.PassengerId, type, title, body, trip.Id, b.Id, ct);
    }

    private static void UpdateBookingProgress(IEnumerable<Booking> bookings, BookingProgressStatus progress, DateTimeOffset now)
    {
        foreach (var b in bookings.Where(x => x.Status == BookingStatus.Accepted))
        {
            b.ProgressStatus = progress;
            b.ProgressUpdatedAt = now;
            b.UpdatedAt = now;
        }
    }

    private static double SumRouteDistanceKm(List<TripRoutePoint> orderedPoints, int startIndex, int endIndex)
    {
        startIndex = Math.Clamp(startIndex, 0, orderedPoints.Count - 1);
        endIndex = Math.Clamp(endIndex, 0, orderedPoints.Count - 1);
        if (endIndex <= startIndex) return 0;

        double km = 0;
        for (int i = startIndex; i < endIndex; i++)
        {
            var a = orderedPoints[i];
            var b = orderedPoints[i + 1];
            km += GeoHelper.DistanceInKm(a.Lat, a.Lng, b.Lat, b.Lng);
        }
        return km;
    }

}
