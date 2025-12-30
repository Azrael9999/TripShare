using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TripShare.Application.Contracts;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Services;

public sealed class SafetyService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;
    private readonly MessagingService _messaging;
    private readonly ILogger<SafetyService> _log;

    public SafetyService(AppDbContext db, NotificationService notifications, MessagingService messaging, ILogger<SafetyService> log)
    {
        _db = db;
        _notifications = notifications;
        _messaging = messaging;
        _log = log;
    }

    public Task<EmergencyContact?> GetEmergencyContactAsync(Guid userId, CancellationToken ct = default)
        => _db.EmergencyContacts.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);

    public async Task<EmergencyContact> UpsertEmergencyContactAsync(Guid userId, EmergencyContactRequest req, CancellationToken ct = default)
    {
        var contact = await _db.EmergencyContacts.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (contact is null)
        {
            contact = new EmergencyContact
            {
                UserId = userId,
                Name = req.Name,
                PhoneNumber = req.PhoneNumber,
                Email = req.Email,
                ShareLiveTripsByDefault = req.ShareLiveTripsByDefault
            };
            _db.EmergencyContacts.Add(contact);
        }
        else
        {
            contact.Name = req.Name;
            contact.PhoneNumber = req.PhoneNumber;
            contact.Email = req.Email;
            contact.ShareLiveTripsByDefault = req.ShareLiveTripsByDefault;
            contact.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return contact;
    }

    public async Task RemoveEmergencyContactAsync(Guid userId, CancellationToken ct = default)
    {
        var contact = await _db.EmergencyContacts.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (contact is null) return;
        _db.EmergencyContacts.Remove(contact);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TripShareLink> CreateShareLinkAsync(Guid userId, CreateShareLinkRequest req, CancellationToken ct = default)
    {
        var trip = await _db.Trips.Include(x => x.Bookings).FirstOrDefaultAsync(x => x.Id == req.TripId, ct)
            ?? throw new InvalidOperationException("Trip not found.");

        var participant = trip.DriverId == userId || trip.Bookings.Any(x => x.PassengerId == userId);
        if (!participant)
            throw new UnauthorizedAccessException("You are not part of this trip.");

        if (req.EmergencyContactId is not null)
        {
            var contactExists = await _db.EmergencyContacts.AnyAsync(x => x.Id == req.EmergencyContactId && x.UserId == userId, ct);
            if (!contactExists) throw new InvalidOperationException("Emergency contact not found.");
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(36));
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(req.ExpiresMinutes, 15, 24 * 60));
        var link = new TripShareLink
        {
            TripId = req.TripId,
            CreatedByUserId = userId,
            EmergencyContactId = req.EmergencyContactId,
            Token = token,
            ExpiresAt = expiresAt
        };
        _db.TripShareLinks.Add(link);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Share link created for trip {TripId} by {UserId}", req.TripId, userId);
        return link;
    }

    public async Task<object?> GetShareLinkSnapshotAsync(Guid tripId, string token, CancellationToken ct = default)
    {
        var link = await _db.TripShareLinks.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TripId == tripId && x.Token == token, ct);

        if (link is null || !link.IsActive)
            return null;

        var trip = await _db.Trips.AsNoTracking().Include(x => x.Driver).SingleOrDefaultAsync(x => x.Id == tripId, ct);
        if (trip == null) return null;

        return new
        {
            trip.Id,
            driver = trip.Driver?.DisplayName,
            photoUrl = trip.Driver?.PhotoUrl,
            lat = trip.CurrentLat,
            lng = trip.CurrentLng,
            heading = trip.CurrentHeading,
            status = trip.Status,
            updatedAt = trip.LocationUpdatedAt
        };
    }

    public async Task<SafetyIncident> CreateIncidentAsync(Guid userId, PanicRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Summary))
            throw new InvalidOperationException("Summary is required.");

        if (req.BookingId is null && req.TripId is null)
            throw new InvalidOperationException("A trip or booking is required for escalation.");

        if (req.BookingId is not null)
        {
            var booking = await _db.Bookings.Include(x => x.Trip).AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.BookingId, ct)
                ?? throw new InvalidOperationException("Booking not found.");
            if (booking.PassengerId != userId && booking.Trip?.DriverId != userId)
                throw new UnauthorizedAccessException("You are not part of this booking.");
        }
        else if (req.TripId is not null)
        {
            var trip = await _db.Trips.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.TripId, ct)
                ?? throw new InvalidOperationException("Trip not found.");
            if (trip.DriverId != userId)
                throw new UnauthorizedAccessException("You are not part of this trip.");
        }

        var incident = new SafetyIncident
        {
            UserId = userId,
            TripId = req.TripId,
            BookingId = req.BookingId,
            Type = req.Type,
            Summary = req.Summary.Trim(),
            Status = SafetyIncidentStatus.Escalated,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            EscalatedAt = DateTimeOffset.UtcNow
        };
        _db.SafetyIncidents.Add(incident);
        await _db.SaveChangesAsync(ct);

        await NotifyCounterpartyAsync(req.BookingId, userId, req.TripId, req.Summary, ct);
        await NotifyAdminsAsync(req.TripId, req.BookingId, req.Summary, ct);
        return incident;
    }

    public async Task<List<SafetyIncident>> ListIncidentsAsync(SafetyIncidentStatus? status, int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var q = _db.SafetyIncidents.AsNoTracking();
        if (status.HasValue) q = q.Where(x => x.Status == status.Value);
        return await q.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
    }

    public async Task ResolveIncidentAsync(Guid incidentId, Guid resolverId, SafetyIncidentStatus status, string? note, CancellationToken ct = default)
    {
        var incident = await _db.SafetyIncidents.FirstOrDefaultAsync(x => x.Id == incidentId, ct);
        if (incident is null) return;
        incident.Status = status;
        incident.ResolvedAt = DateTimeOffset.UtcNow;
        incident.ResolvedByUserId = resolverId;
        incident.ResolutionNote = note;
        incident.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task NotifyCounterpartyAsync(Guid? bookingId, Guid userId, Guid? tripId, string summary, CancellationToken ct)
    {
        if (bookingId is null) return;

        var booking = await _db.Bookings.Include(x => x.Trip).FirstOrDefaultAsync(x => x.Id == bookingId.Value, ct);
        if (booking?.Trip == null) return;

        var otherUserId = booking.PassengerId == userId ? booking.Trip.DriverId : booking.PassengerId;
        await _notifications.CreateAsync(otherUserId, NotificationType.SafetyAlert, "Safety alert raised", summary, booking.TripId, booking.Id, ct);

        // Create a system message in the shared thread for visibility
        var thread = await _messaging.GetOrCreateBookingThreadAsync(booking.Id, userId, ct);
        await _messaging.SendSystemAsync(userId, thread.Id, "[automated] Panic triggered by participant. Safety team has been alerted.", ct);
    }

    private async Task NotifyAdminsAsync(Guid? tripId, Guid? bookingId, string summary, CancellationToken ct)
    {
        var adminIds = await _db.Users.AsNoTracking().Where(x => x.Role == "admin").Select(x => x.Id).ToListAsync(ct);
        foreach (var adminId in adminIds)
        {
            await _notifications.CreateAsync(adminId, NotificationType.SafetyAlert, "Safety incident escalated", summary, tripId, bookingId, ct);
        }
    }
}
