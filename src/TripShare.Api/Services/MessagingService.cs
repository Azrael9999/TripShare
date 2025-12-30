using Microsoft.EntityFrameworkCore;
using TripShare.Application.Contracts;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Services;

public sealed class MessagingService
{
    private readonly AppDbContext _db;
    private readonly BlockService _blocks;
    private readonly NotificationService _notifications;
    private readonly ILogger<MessagingService> _log;

    public MessagingService(AppDbContext db, BlockService blocks, NotificationService notifications, ILogger<MessagingService> log)
    {
        _db = db;
        _blocks = blocks;
        _notifications = notifications;
        _log = log;
    }

    public async Task<MessageThread> GetOrCreateBookingThreadAsync(Guid bookingId, Guid requesterId, CancellationToken ct = default)
    {
        var booking = await _db.Bookings.Include(x => x.Trip).FirstOrDefaultAsync(x => x.Id == bookingId, ct)
            ?? throw new InvalidOperationException("Booking not found.");
        if (booking.Trip == null)
            throw new InvalidOperationException("Trip not found for booking.");

        if (booking.PassengerId != requesterId && booking.Trip.DriverId != requesterId)
            throw new UnauthorizedAccessException("You are not part of this booking.");

        var otherUserId = booking.PassengerId == requesterId ? booking.Trip.DriverId : booking.PassengerId;
        await EnsureNotBlocked(requesterId, otherUserId, ct);

        var thread = await _db.MessageThreads.FirstOrDefaultAsync(x => x.BookingId == bookingId, ct);
        if (thread is null)
        {
            thread = new MessageThread
            {
                BookingId = bookingId,
                TripId = booking.TripId,
                ParticipantAId = requesterId,
                ParticipantBId = otherUserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.MessageThreads.Add(thread);
            await _db.SaveChangesAsync(ct);
        }

        return thread;
    }

    public async Task<List<MessageThread>> ListThreadsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.MessageThreads.AsNoTracking()
            .Where(x => x.ParticipantAId == userId || x.ParticipantBId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(100)
            .ToListAsync(ct);
    }

    public async Task<List<Message>> ListMessagesAsync(Guid threadId, Guid userId, int take, CancellationToken ct = default)
    {
        _ = await EnsureParticipantAsync(threadId, userId, ct);
        take = Math.Clamp(take, 1, 200);
        return await _db.Messages.AsNoTracking()
            .Where(x => x.ThreadId == threadId)
            .OrderByDescending(x => x.SentAt)
            .Take(take)
            .OrderBy(x => x.SentAt)
            .ToListAsync(ct);
    }

    public async Task<Message> SendAsync(Guid senderId, Guid threadId, string body, bool isSystem, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("Message body is required.");

        var thread = await EnsureParticipantAsync(threadId, senderId, ct);
        if (thread.IsClosed)
            throw new InvalidOperationException("Thread is closed.");

        var recipient = thread.ParticipantAId == senderId ? thread.ParticipantBId : thread.ParticipantAId;
        await EnsureNotBlocked(senderId, recipient, ct);

        var msg = new Message
        {
            ThreadId = threadId,
            SenderId = senderId,
            Body = body.Trim(),
            IsSystem = isSystem,
            SentAt = DateTimeOffset.UtcNow
        };
        _db.Messages.Add(msg);
        thread.UpdatedAt = msg.SentAt;

        await _db.SaveChangesAsync(ct);

        await _notifications.CreateAsync(recipient, NotificationType.DirectMessage, "New message", "You have a new in-app message.", thread.TripId, thread.BookingId, ct);
        _log.LogInformation("Message sent thread={ThreadId} from={Sender} to={Recipient}", threadId, senderId, recipient);
        return msg;
    }

    public async Task<Message> SendSystemAsync(Guid actorId, Guid threadId, string body, CancellationToken ct = default)
        => await SendAsync(actorId, threadId, body, isSystem: true, ct);

    private async Task<MessageThread> EnsureParticipantAsync(Guid threadId, Guid userId, CancellationToken ct)
    {
        var thread = await _db.MessageThreads.FirstOrDefaultAsync(x => x.Id == threadId, ct)
            ?? throw new InvalidOperationException("Thread not found.");
        if (thread.ParticipantAId != userId && thread.ParticipantBId != userId)
            throw new UnauthorizedAccessException("You are not part of this conversation.");
        return thread;
    }

    private async Task EnsureNotBlocked(Guid senderId, Guid recipientId, CancellationToken ct)
    {
        var senderBlocks = await _blocks.GetBlockedUserIdsAsync(senderId, ct);
        if (senderBlocks.Contains(recipientId))
            throw new InvalidOperationException("You have blocked this user.");
        var recipientBlocks = await _blocks.GetBlockedUserIdsAsync(recipientId, ct);
        if (recipientBlocks.Contains(senderId))
            throw new InvalidOperationException("You cannot message a user who blocked you.");
    }
}
