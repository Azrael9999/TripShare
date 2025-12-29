using Microsoft.EntityFrameworkCore;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Services;

public sealed class BlockService
{
    private readonly AppDbContext _db;

    public BlockService(AppDbContext db) => _db = db;

    public async Task BlockAsync(Guid blockerId, Guid blockedId, CancellationToken ct = default)
    {
        if (blockerId == blockedId) return;
        var exists = await _db.UserBlocks.AnyAsync(x => x.BlockerUserId == blockerId && x.BlockedUserId == blockedId, ct);
        if (exists) return;
        _db.UserBlocks.Add(new UserBlock { BlockerUserId = blockerId, BlockedUserId = blockedId, CreatedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync(ct);
    }

    public async Task UnblockAsync(Guid blockerId, Guid blockedId, CancellationToken ct = default)
    {
        var rows = await _db.UserBlocks.Where(x => x.BlockerUserId == blockerId && x.BlockedUserId == blockedId).ToListAsync(ct);
        if (rows.Count == 0) return;
        _db.UserBlocks.RemoveRange(rows);
        await _db.SaveChangesAsync(ct);
    }

    

    public async Task<List<object>> ListAsync(Guid blockerId, CancellationToken ct = default)
    {
        var q = from b in _db.UserBlocks.AsNoTracking()
                join u in _db.Users.AsNoTracking() on b.BlockedUserId equals u.Id
                where b.BlockerUserId == blockerId
                orderby u.DisplayName
                select new { id = u.Id, displayName = u.DisplayName, photoUrl = u.PhotoUrl, emailVerified = u.EmailVerified };

        return await q.Cast<object>().ToListAsync(ct);
    }

public async Task<HashSet<Guid>> GetBlockedUserIdsAsync(Guid blockerId, CancellationToken ct = default)
    {
        var ids = await _db.UserBlocks.AsNoTracking().Where(x => x.BlockerUserId == blockerId).Select(x => x.BlockedUserId).ToListAsync(ct);
        return ids.ToHashSet();
    }
}
