using Microsoft.EntityFrameworkCore;
using TripShare.Application.Contracts;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Services;

public sealed class IdentityVerificationService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;
    private readonly ILogger<IdentityVerificationService> _log;

    public IdentityVerificationService(AppDbContext db, NotificationService notifications, ILogger<IdentityVerificationService> log)
    {
        _db = db;
        _notifications = notifications;
        _log = log;
    }

    public async Task<IdentityVerificationRequest> SubmitAsync(Guid userId, IdentityVerificationSubmission submission, CancellationToken ct = default)
    {
        var existing = await _db.IdentityVerificationRequests
            .Where(x => x.UserId == userId && x.Status == IdentityVerificationStatus.Pending)
            .OrderByDescending(x => x.SubmittedAt)
            .FirstOrDefaultAsync(ct);

        if (existing != null) return existing;

        var req = new IdentityVerificationRequest
        {
            UserId = userId,
            DocumentType = submission.DocumentType,
            DocumentReference = submission.DocumentReference,
            KycProvider = submission.KycProvider,
            KycReference = submission.KycReference
        };
        _db.IdentityVerificationRequests.Add(req);
        await _db.SaveChangesAsync(ct);

        var admins = await _db.Users.AsNoTracking().Where(x => x.Role == "admin").Select(x => x.Id).ToListAsync(ct);
        foreach (var admin in admins)
        {
            await _notifications.CreateAsync(admin, NotificationType.IdentityVerification, "Identity verification submitted", $"User {userId} submitted verification details.", null, null, ct);
        }

        _log.LogInformation("Identity verification submitted by user {UserId}", userId);
        return req;
    }

    public async Task<List<IdentityVerificationRequest>> ListAsync(IdentityVerificationStatus? status, int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var q = _db.IdentityVerificationRequests.AsNoTracking();
        if (status.HasValue) q = q.Where(x => x.Status == status.Value);
        return await q.OrderByDescending(x => x.SubmittedAt).Take(take).ToListAsync(ct);
    }

    public async Task<IdentityVerificationRequest?> GetLatestAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.IdentityVerificationRequests.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.SubmittedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task ReviewAsync(Guid requestId, Guid reviewerId, IdentityVerificationReview review, CancellationToken ct = default)
    {
        var req = await _db.IdentityVerificationRequests.FirstOrDefaultAsync(x => x.Id == requestId, ct)
            ?? throw new InvalidOperationException("Request not found.");

        req.Status = review.Approve ? IdentityVerificationStatus.Approved : IdentityVerificationStatus.Rejected;
        req.ReviewedAt = DateTimeOffset.UtcNow;
        req.ReviewerNote = review.Note;
        req.ReviewedByUserId = reviewerId;

        var user = await _db.Users.FirstAsync(x => x.Id == req.UserId, ct);
        user.IdentityVerified = review.Approve;
        user.IdentityVerifiedAt = review.Approve ? req.ReviewedAt : null;
        if (review.Approve && user.IsDriver)
        {
            user.DriverVerified = true;
            user.DriverVerifiedAt = req.ReviewedAt;
        }

        await _db.SaveChangesAsync(ct);

        var title = review.Approve ? "Identity verified" : "Identity verification rejected";
        var body = review.Approve ? "Your identity verification was approved." : $"Verification rejected: {review.Note}";
        await _notifications.CreateAsync(user.Id, NotificationType.IdentityVerification, title, body, null, null, ct);
    }
}
