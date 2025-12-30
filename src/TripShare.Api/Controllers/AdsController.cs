using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using TripShare.Api.Services;
using TripShare.Application.Contracts;
using TripShare.Api.Helpers;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/ads")]
public sealed class AdsController : ControllerBase
{
    private readonly SiteSettingsService _settings;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AdsController> _log;

    public AdsController(SiteSettingsService settings, IMemoryCache cache, ILogger<AdsController> log)
    {
        _settings = settings;
        _cache = cache;
        _log = log;
    }

    [AllowAnonymous]
    [HttpGet("config")]
    public async Task<ActionResult<AdConfigurationDto>> GetConfig(CancellationToken ct)
    {
        var config = await _settings.GetAdConfigurationAsync(ct);
        // Only return enabled slots to clients
        var filtered = new AdConfigurationDto(
            config.Enabled,
            config.FrequencyCapPerSession,
            config.MaxSlotsPerPage,
            config.Slots.Where(s => s.Enabled).ToList());
        return Ok(filtered);
    }

    [AllowAnonymous]
    [HttpPost("impression")]
    public async Task<IActionResult> RecordImpression([FromBody] AdImpressionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Slot))
            return BadRequest(new { message = "Slot is required." });

        var config = await _settings.GetAdConfigurationAsync(ct);
        if (!config.Enabled)
            return NoContent();

        var slot = config.Slots.FirstOrDefault(s => s.Slot == req.Slot && s.Enabled);
        if (slot is null)
            return BadRequest(new { message = "Unknown or disabled slot." });

        var sessionKey = BuildSessionKey(req.SessionId);
        if (string.IsNullOrEmpty(sessionKey))
            return BadRequest(new { message = "Unable to derive session." });

        var cacheKey = $"ad-impr:{sessionKey}:{slot.Slot}";
        var count = _cache.GetOrCreate<int>(cacheKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(6);
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            return 0;
        });

        if (count >= config.FrequencyCapPerSession && config.FrequencyCapPerSession > 0)
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Frequency cap reached." });

        var updated = count + 1;
        _cache.Set(cacheKey, updated, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(6),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12)
        });

        _log.LogInformation("Ad impression recorded slot={Slot} session={Session} count={Count}", slot.Slot, sessionKey, updated);
        return NoContent();
    }

    private string BuildSessionKey(string? provided)
    {
        if (!string.IsNullOrWhiteSpace(provided))
            return provided!;

        var uid = User?.Identity?.IsAuthenticated == true ? User.GetUserId().ToString() : null;
        if (!string.IsNullOrEmpty(uid))
            return $"uid:{uid}";

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        var ua = Request.Headers.UserAgent.ToString();
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"{ip}|{ua}"));
        return Convert.ToHexString(hash);
    }
}
