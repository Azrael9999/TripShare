using Microsoft.EntityFrameworkCore;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Services;

public sealed class ReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db) => _db = db;

    public async Task<Report> CreateAsync(Report report, CancellationToken ct = default)
    {
        report.CreatedAt = DateTimeOffset.UtcNow;
        report.UpdatedAt = report.CreatedAt;
        _db.Reports.Add(report);
        await _db.SaveChangesAsync(ct);
        return report;
    }

    public async Task<List<Report>> ListAsync(ReportStatus? status, int take, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var q = _db.Reports.AsNoTracking();
        if (status.HasValue) q = q.Where(x => x.Status == status.Value);
        return await q.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
    }

    public async Task UpdateStatusAsync(Guid reportId, ReportStatus status, string? adminNote, CancellationToken ct = default)
    {
        var r = await _db.Reports.FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (r == null) return;
        r.Status = status;
        r.AdminNote = adminNote;
        r.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
