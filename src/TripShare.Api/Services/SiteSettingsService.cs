using Microsoft.EntityFrameworkCore;
using TripShare.Infrastructure.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using TripShare.Application.Contracts;

namespace TripShare.Api.Services;

public sealed class SiteSettingsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SiteSettingsService> _log;

    private const string DriverVerificationRequiredKey = "DriverVerificationRequired";
    private const string AdConfigKey = "AdConfig";
    private const string BrandingConfigKey = "BrandingConfig";

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
            return parsed is null ? DefaultAdConfiguration() : Normalize(parsed);
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

    public async Task<BrandingConfigDto?> GetBrandingConfigAsync(CancellationToken ct = default)
    {
        var raw = await _db.SiteSettings.AsNoTracking().Where(x => x.Key == BrandingConfigKey).Select(x => x.Value).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BrandingConfigDto>(raw);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to parse branding configuration.");
            return null;
        }
    }

    public async Task SetBrandingConfigAsync(BrandingConfigDto config, CancellationToken ct = default)
    {
        ValidateBrandingConfig(config);
        var now = DateTimeOffset.UtcNow;
        var raw = JsonSerializer.Serialize(NormalizeBranding(config));
        var setting = await _db.SiteSettings.FirstOrDefaultAsync(x => x.Key == BrandingConfigKey, ct);
        if (setting is null)
        {
            setting = new Domain.Entities.SiteSetting
            {
                Key = BrandingConfigKey,
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
        _log.LogInformation("Branding configuration updated.");
    }

    private static void ValidateAdConfiguration(AdConfigurationDto config)
    {
        if (config.FrequencyCapPerSession < 0 || config.FrequencyCapPerSession > 100)
            throw new InvalidOperationException("Frequency cap must be between 0-100 per session.");

        if (config.MaxSlotsPerPage < 0 || config.MaxSlotsPerPage > 10)
            throw new InvalidOperationException("Max slots per page must be between 0-10.");

        if (config.Slots is null)
            throw new InvalidOperationException("Ad slots collection is required (can be empty list).");

        foreach (var slot in config.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Slot))
                throw new InvalidOperationException("Ad slot name is required.");

            if (slot.Html.Length > 2000)
                throw new InvalidOperationException("Ad slot HTML too long (max 2000 chars).");

            EnsureSafeMarkup(slot.Html);
        }
    }

    private static AdConfigurationDto DefaultAdConfiguration()
        => new(false, 3, 3, new List<AdSlotDto>
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

    private static void EnsureSafeMarkup(string html)
    {
        var normalized = html ?? string.Empty;
        if (Regex.IsMatch(normalized, @"(?is)<\s*script") ||
            Regex.IsMatch(normalized, @"(?is)<\s*(iframe|object|embed|applet)"))
            throw new InvalidOperationException("Scriptable or embedded elements are not allowed in ad markup.");

        if (Regex.IsMatch(normalized, @"(?is)javascript:") || Regex.IsMatch(normalized, @"(?is)data:text/html"))
            throw new InvalidOperationException("Potentially unsafe URLs are not allowed in ad markup.");

        if (Regex.IsMatch(normalized, @"(?is)on[a-z]+\s*="))
            throw new InvalidOperationException("Inline event handlers are not allowed in ad markup.");
    }

    private static AdConfigurationDto Normalize(AdConfigurationDto config)
    {
        var defaults = DefaultAdConfiguration();
        var maxSlots = config.MaxSlotsPerPage <= 0 ? defaults.MaxSlotsPerPage : config.MaxSlotsPerPage;
        var freq = config.FrequencyCapPerSession < 0 ? defaults.FrequencyCapPerSession : config.FrequencyCapPerSession;
        var slots = config.Slots ?? new List<AdSlotDto>();
        return config with { MaxSlotsPerPage = maxSlots, FrequencyCapPerSession = freq, Slots = slots };
    }

    private static void ValidateBrandingConfig(BrandingConfigDto config)
    {
        var maxLen = 200000;
        if (config.LogoUrl?.Length > maxLen || config.HeroImageUrl?.Length > maxLen ||
            config.MapOverlayUrl?.Length > maxLen || config.LoginIllustrationUrl?.Length > maxLen)
        {
            throw new InvalidOperationException("Branding image payload too large.");
        }
    }

    private static BrandingConfigDto NormalizeBranding(BrandingConfigDto config)
        => new(
            NormalizeNullable(config.LogoUrl),
            NormalizeNullable(config.HeroImageUrl),
            NormalizeNullable(config.MapOverlayUrl),
            NormalizeNullable(config.LoginIllustrationUrl));

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
