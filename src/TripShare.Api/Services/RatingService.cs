using Microsoft.EntityFrameworkCore;
using TripShare.Application.Contracts;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Services;

public sealed class RatingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RatingService> _log;

    public RatingService(AppDbContext db, ILogger<RatingService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task CreateAsync(Guid fromUserId, CreateRatingRequest req, CancellationToken ct)
    {
        if (req.Stars < 1 || req.Stars > 5) throw new InvalidOperationException("Stars must be 1..5.");

        var booking = await _db.Bookings
            .Include(x => x.Trip)
            .SingleOrDefaultAsync(x => x.Id == req.BookingId, ct);

        if (booking?.Trip is null) throw new InvalidOperationException("Booking not found.");
        if (booking.Status != BookingStatus.Completed) throw new InvalidOperationException("Rating allowed only after trip completion.");

        var toUserId = booking.Trip.DriverId == fromUserId ? booking.PassengerId : booking.Trip.DriverId;
        if (toUserId == Guid.Empty) throw new InvalidOperationException("Invalid rating target.");

        // must be participant
        if (booking.PassengerId != fromUserId && booking.Trip.DriverId != fromUserId)
            throw new UnauthorizedAccessException();

        var exists = await _db.Ratings.AnyAsync(x => x.BookingId == req.BookingId && x.FromUserId == fromUserId, ct);
        if (exists) throw new InvalidOperationException("You already rated for this booking.");

        _db.Ratings.Add(new Rating
        {
            BookingId = req.BookingId,
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Stars = req.Stars,
            Comment = req.Comment
        });

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Rating created for booking {BookingId} from {From} to {To}", req.BookingId, fromUserId, toUserId);
    }

    public async Task<double> GetUserAverageAsync(Guid userId, CancellationToken ct)
    {
        var q = _db.Ratings.AsNoTracking().Where(x => x.ToUserId == userId);
        var count = await q.CountAsync(ct);
        if (count == 0) return 0;
        return await q.AverageAsync(x => (double)x.Stars, ct);
    }
}
