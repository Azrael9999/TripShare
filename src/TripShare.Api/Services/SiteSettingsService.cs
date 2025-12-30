using Microsoft.EntityFrameworkCore;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Services;

public sealed class SiteSettingsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SiteSettingsService> _log;

    private const string DriverVerificationRequiredKey = "DriverVerificationRequired";

    public SiteSettingsService(AppDbContext db, ILogger<SiteSettingsService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<bool> GetDriverVerificationRequiredAsync(CancellationToken ct = default)
    {
        var value = await _db.SiteSettings
            .AsNoTracking()
            .Where(x => x.Key == DriverVerificationRequiredKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);

        return bool.TryParse(value, out var required) && required;
    }

    public async Task SetDriverVerificationRequiredAsync(bool required, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var setting = await _db.SiteSettings.FirstOrDefaultAsync(x => x.Key == DriverVerificationRequiredKey, ct);
        if (setting is null)
        {
            setting = new Domain.Entities.SiteSetting
            {
                Key = DriverVerificationRequiredKey,
                Value = required.ToString().ToLowerInvariant(),
                UpdatedAt = now
            };
            _db.SiteSettings.Add(setting);
        }
        else
        {
            setting.Value = required.ToString().ToLowerInvariant();
            setting.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Driver verification required flag set to {Required}", required);
    }
}
