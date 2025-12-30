using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripShare.Api.Services;
using TripShare.Application.Contracts;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/ads")]
public sealed class AdsController : ControllerBase
{
    private readonly SiteSettingsService _settings;

    public AdsController(SiteSettingsService settings)
    {
        _settings = settings;
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
            config.Slots.Where(s => s.Enabled).ToList());
        return Ok(filtered);
    }
}
