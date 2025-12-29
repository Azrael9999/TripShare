using Microsoft.EntityFrameworkCore;
using TripShare.Application.Contracts;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Services;

public sealed class BookingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<BookingService> _log;
    private readonly NotificationService _notif;

    public BookingService(AppDbContext db, ILogger<BookingService> log, NotificationService notif)
    {
        _db = db;
        _log = log;
        _notif = notif;
    }

    public async Task<BookingDto> CreateAsync(Guid passengerId, CreateBookingRequest req, CancellationToken ct)
    {
        if (req.Seats < 1 || req.Seats > 8)
            throw new InvalidOperationException("Seats must be 1..8.");

        // Load trip graph
        var trip = await _db.Trips
            .Include(x => x.RoutePoints)
            .Include(x => x.Segments)
            .SingleOrDefaultAsync(x => x.Id == req.TripId && x.IsPublic, ct);

        if (trip is null) throw new InvalidOperationException("Trip not found.");
        if (trip.DriverId == passengerId) throw new InvalidOperationException("Driver cannot book own trip.");

        var pickup = trip.RoutePoints.SingleOrDefault(x => x.Id == req.PickupRoutePointId);
        var drop = trip.RoutePoints.SingleOrDefault(x => x.Id == req.DropoffRoutePointId);
        if (pickup is null || drop is null) throw new InvalidOperationException("Invalid pickup/dropoff.");
        if (drop.OrderIndex <= pickup.OrderIndex) throw new InvalidOperationException("Dropoff must be after pickup.");

        var startSegIndex = pickup.OrderIndex;
        var endSegIndex = drop.OrderIndex - 1;

        var segs = trip.Segments
            .Where(s => s.OrderIndex >= startSegIndex && s.OrderIndex <= endSegIndex)
            .OrderBy(s => s.OrderIndex)
            .ToList();

        if (segs.Count == 0) throw new InvalidOperationException("No segments for selection.");

        var perSeat = segs.Sum(s => s.Price);
        var total = perSeat * req.Seats;

        // Transaction + optimistic concurrency retries
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            try
            {
                // re-load segments with tracking for concurrency
                var segIds = segs.Select(s => s.Id).ToList();
                var trackedSegs = await _db.TripSegments
                    .Where(s => segIds.Contains(s.Id))
                    .OrderBy(s => s.OrderIndex)
                    .ToListAsync(ct);

                foreach (var s in trackedSegs)
                {
                    if (s.BookedSeats + req.Seats > trip.SeatsTotal)
                        throw new InvalidOperationException("Not enough seats for selected section.");

                    s.BookedSeats += req.Seats;
                }

                var booking = new Booking
                {
                    TripId = trip.Id,
                    PassengerId = passengerId,
                    PickupRoutePointId = pickup.Id,
                    DropoffRoutePointId = drop.Id,
                    Seats = req.Seats,
                    PriceTotal = total,
                    Currency = trip.Currency,
                    Status = trip.InstantBook ? BookingStatus.Accepted : BookingStatus.Pending,
                    PendingExpiresAt = trip.InstantBook
                        ? null
                        : DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, trip.PendingBookingExpiryMinutes))
                };

                _db.Bookings.Add(booking);

                foreach (var s in trackedSegs)
                {
                    _db.BookingSegmentAllocations.Add(new BookingSegmentAllocation
                    {
                        BookingId = booking.Id,
                        TripSegmentId = s.Id,
                        Seats = req.Seats
                    });
                }

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                // Notifications (creation-time only)
                if (trip.InstantBook)
                {
                    await _notif.CreateAsync(
                        passengerId,
                        NotificationType.BookingAccepted,
                        "Booking accepted",
                        "Your booking was accepted automatically.",
                        trip.Id,
                        booking.Id,
                        ct);

                    await _notif.CreateAsync(
                        trip.DriverId,
                        NotificationType.BookingAccepted,
                        "New booking",
                        "A new booking was accepted automatically.",
                        trip.Id,
                        booking.Id,
                        ct);
                }
                else
                {
                    await _notif.CreateAsync(
                        trip.DriverId,
                        NotificationType.BookingRequested,
                        "New booking request",
                        "A passenger requested to join your trip.",
                        trip.Id,
                        booking.Id,
                        ct);

                    await _notif.CreateAsync(
                        passengerId,
                        NotificationType.BookingRequested,
                        "Booking requested",
                        "Your booking request was sent to the driver.",
                        trip.Id,
                        booking.Id,
                        ct);
                }

                _log.LogInformation(
                    "Booking {BookingId} created for trip {TripId} by {PassengerId}",
                    booking.Id, trip.Id, passengerId);

                return Map(booking);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await tx.RollbackAsync(ct);
                _log.LogWarning(ex, "Concurrency conflict on booking create attempt {Attempt}", attempt);

                if (attempt == 3)
                    throw new InvalidOperationException("Please try again.");
            }
        }

        throw new InvalidOperationException("Please try again.");
    }

    public async Task<List<BookingDto>> MyBookingsAsync(Guid userId, CancellationToken ct)
    {
        var items = await _db.Bookings.AsNoTracking()
            .Where(x => x.PassengerId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return items.Select(Map).ToList();
    }

    public async Task<List<BookingDto>> DriverBookingsAsync(Guid driverId, CancellationToken ct)
    {
        var items = await _db.Bookings.AsNoTracking()
            .Include(x => x.Trip)
            .Where(x => x.Trip!.DriverId == driverId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return items.Select(Map).ToList();
    }

    public async Task SetStatusAsync(
        Guid actorId,
        Guid bookingId,
        SetBookingStatusRequest req,
        bool isDriverAction,
        CancellationToken ct)
    {
        var booking = await _db.Bookings
            .Include(x => x.Trip)
            .Include(x => x.SegmentAllocations)
            .SingleOrDefaultAsync(x => x.Id == bookingId, ct);

        if (booking?.Trip is null) throw new InvalidOperationException("Booking not found.");

        if (isDriverAction)
        {
            if (booking.Trip.DriverId != actorId) throw new UnauthorizedAccessException();
        }
        else
        {
            if (booking.PassengerId != actorId) throw new UnauthorizedAccessException();
        }

        var target = (req.Status ?? "").Trim().ToLowerInvariant();
        var newStatus = target switch
        {
            "accepted" => BookingStatus.Accepted,
            "rejected" => BookingStatus.Rejected,
            "cancelled" => BookingStatus.Cancelled,
            "completed" => BookingStatus.Completed,
            _ => throw new InvalidOperationException("Invalid status.")
        };

        // Only allow certain transitions
        if (newStatus == BookingStatus.Accepted && booking.Status != BookingStatus.Pending)
            throw new InvalidOperationException("Only pending bookings can be accepted.");
        if (newStatus == BookingStatus.Rejected && booking.Status != BookingStatus.Pending)
            throw new InvalidOperationException("Only pending bookings can be rejected.");
        if (newStatus == BookingStatus.Cancelled && booking.Status is BookingStatus.Rejected or BookingStatus.Completed)
            throw new InvalidOperationException("Cannot cancel in current state.");
        if (newStatus == BookingStatus.Completed && booking.Status != BookingStatus.Accepted)
            throw new InvalidOperationException("Only accepted bookings can be completed.");

        // Apply updates that must be persisted
        booking.CancellationReason = req.Reason;
        booking.Status = newStatus;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        if (newStatus is BookingStatus.Accepted or BookingStatus.Rejected)
            booking.PendingExpiresAt = null;

        if (newStatus == BookingStatus.Completed)
            booking.CompletedAt ??= DateTimeOffset.UtcNow;

        // If reject/cancel: release seats within a serializable transaction
        if (newStatus is BookingStatus.Rejected or BookingStatus.Cancelled)
        {
            using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

            var segIds = booking.SegmentAllocations.Select(a => a.TripSegmentId).ToList();
            var segs = await _db.TripSegments
                .Where(s => segIds.Contains(s.Id))
                .ToListAsync(ct);

            foreach (var s in segs)
                s.BookedSeats = Math.Max(0, s.BookedSeats - booking.Seats);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        else
        {
            await _db.SaveChangesAsync(ct);
        }

        // Notifications (after DB state is successfully persisted)
        if (newStatus == BookingStatus.Accepted)
        {
            await _notif.CreateAsync(
                booking.PassengerId,
                NotificationType.BookingAccepted,
                "Booking accepted",
                "Your booking request was accepted by the driver.",
                booking.TripId,
                booking.Id,
                ct);
        }
        else if (newStatus == BookingStatus.Rejected)
        {
            await _notif.CreateAsync(
                booking.PassengerId,
                NotificationType.BookingRejected,
                "Booking rejected",
                "Your booking request was rejected by the driver.",
                booking.TripId,
                booking.Id,
                ct);
        }
        else if (newStatus == BookingStatus.Cancelled)
        {
            await _notif.CreateAsync(
                booking.PassengerId,
                NotificationType.BookingCancelled,
                "Booking cancelled",
                "Booking was cancelled.",
                booking.TripId,
                booking.Id,
                ct);

            await _notif.CreateAsync(
                booking.Trip.DriverId,
                NotificationType.BookingCancelled,
                "Booking cancelled",
                "A booking was cancelled.",
                booking.TripId,
                booking.Id,
                ct);
        }
        else if (newStatus == BookingStatus.Completed)
        {
            await _notif.CreateAsync(
                booking.PassengerId,
                NotificationType.TripCompleted,
                "Trip completed",
                "Trip completed. You can leave a rating now.",
                booking.TripId,
                booking.Id,
                ct);

            await _notif.CreateAsync(
                booking.Trip.DriverId,
                NotificationType.TripCompleted,
                "Trip completed",
                "A booking was completed. You can receive ratings now.",
                booking.TripId,
                booking.Id,
                ct);
        }

        _log.LogInformation(
            "Booking {BookingId} status changed to {Status} by {ActorId}",
            bookingId, newStatus, actorId);
    }

    public async Task<object> RevealContactAsync(Guid actorId, Guid bookingId, CancellationToken ct)
    {
        var b = await _db.Bookings
            .Include(x => x.Trip)
            .Include(x => x.Passenger)
            .FirstOrDefaultAsync(x => x.Id == bookingId, ct);

        if (b?.Trip is null || b.Passenger is null) throw new InvalidOperationException("Booking not found.");
        if (actorId != b.PassengerId && actorId != b.Trip.DriverId) throw new UnauthorizedAccessException();
        if (b.Status != BookingStatus.Accepted)
            throw new InvalidOperationException("Contact details are available only for accepted bookings.");

        var driver = await _db.Users.AsNoTracking().FirstAsync(x => x.Id == b.Trip.DriverId, ct);

        b.ContactRevealed = true;
        b.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _notif.CreateAsync(
            b.PassengerId,
            NotificationType.System,
            "Contact available",
            "Contact details are now available for your trip.",
            b.TripId,
            b.Id,
            ct);

        await _notif.CreateAsync(
            driver.Id,
            NotificationType.System,
            "Contact shared",
            "Passenger has opened contact details for the booking.",
            b.TripId,
            b.Id,
            ct);

        return new
        {
            bookingId = b.Id,
            tripId = b.TripId,
            driver = new { driver.Id, driver.DisplayName, driver.PhoneNumber },
            passenger = new { b.Passenger.Id, b.Passenger.DisplayName, b.Passenger.PhoneNumber }
        };
    }

    private static BookingDto Map(Booking b)
        => new(
            b.Id,
            b.TripId,
            b.PassengerId,
            b.PickupRoutePointId,
            b.DropoffRoutePointId,
            b.Seats,
            b.PriceTotal,
            b.Currency,
            b.Status.ToString(),
            b.CreatedAt);
}
