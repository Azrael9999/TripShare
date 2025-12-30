using Microsoft.EntityFrameworkCore;
using TripShare.Infrastructure.Data;
using System.Text.Json;
using TripShare.Application.Contracts;

namespace TripShare.Api.Services;

public sealed class SiteSettingsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SiteSettingsService> _log;

    private const string DriverVerificationRequiredKey = "DriverVerificationRequired";
    private const string AdConfigKey = "AdConfig";

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

    public async Task<AdConfigurationDto> GetAdConfigurationAsync(CancellationToken ct = default)
    {
        var raw = await _db.SiteSettings.AsNoTracking().Where(x => x.Key == AdConfigKey).Select(x => x.Value).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DefaultAdConfiguration();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<AdConfigurationDto>(raw);
            return parsed ?? DefaultAdConfiguration();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to parse ad configuration, falling back to defaults.");
            return DefaultAdConfiguration();
        }
    }

    public async Task SetAdConfigurationAsync(AdConfigurationDto config, CancellationToken ct = default)
    {
        ValidateAdConfiguration(config);

        var now = DateTimeOffset.UtcNow;
        var raw = JsonSerializer.Serialize(config);
        var setting = await _db.SiteSettings.FirstOrDefaultAsync(x => x.Key == AdConfigKey, ct);
        if (setting is null)
        {
            setting = new Domain.Entities.SiteSetting
            {
                Key = AdConfigKey,
                Value = raw,
                UpdatedAt = now
            };
            _db.SiteSettings.Add(setting);
        }
        else
        {
            setting.Value = raw;
            setting.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Ad configuration updated (enabled={Enabled}, slots={Slots})", config.Enabled, config.Slots.Count);
    }

    private static void ValidateAdConfiguration(AdConfigurationDto config)
    {
        if (config.FrequencyCapPerSession < 0 || config.FrequencyCapPerSession > 100)
            throw new InvalidOperationException("Frequency cap must be between 0-100 per session.");

        foreach (var slot in config.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Slot))
                throw new InvalidOperationException("Ad slot name is required.");

            if (slot.Html.Length > 2000)
                throw new InvalidOperationException("Ad slot HTML too long (max 2000 chars).");
        }
    }

    private static AdConfigurationDto DefaultAdConfiguration()
        => new(false, 3, new List<AdSlotDto>
        {
            new("home-sidebar", "<div class='text-sm text-slate-500'>Ad space</div>", true),
            new("trip-details-bottom", "<div class='text-sm text-slate-500'>Ad space</div>", true),
            new("profile-sidebar", "<div class='text-sm text-slate-500'>Ad space</div>", true),
            new("bookings-sidebar", "<div class='text-sm text-slate-500'>Ad space</div>", true),
            new("notifications-sidebar", "<div class='text-sm text-slate-500'>Ad space</div>", true),
            new("vehicle-sidebar", "<div class='text-sm text-slate-500'>Ad space</div>", true),
            new("create-trip-sidebar", "<div class='text-sm text-slate-500'>Ad space</div>", true),
            new("my-trips-sidebar", "<div class='text-sm text-slate-500'>Ad space</div>", true),
            new("admin-sidebar", "<div class='text-sm text-slate-500'>Ad space</div>", true),
            new("verify-email-sidebar", "<div class='text-sm text-slate-500'>Ad space</div>", true)
        });
}
