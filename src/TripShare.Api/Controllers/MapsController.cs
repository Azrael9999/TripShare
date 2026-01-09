using Microsoft.AspNetCore.Mvc;
using TripShare.Api.Services;
using TripShare.Application.Contracts;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/maps")]
public sealed class MapsController : ControllerBase
{
    private readonly SiteSettingsService _settings;

    public MapsController(SiteSettingsService settings)
    {
        _settings = settings;
    }

    [HttpGet("config")]
    public async Task<ActionResult<MapApiConfigDto>> GetConfig(CancellationToken ct)
        => Ok(await _settings.GetMapApiConfigAsync(ct));
}
