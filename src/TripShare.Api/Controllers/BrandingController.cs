using Microsoft.AspNetCore.Mvc;
using TripShare.Api.Services;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/branding")]
public sealed class BrandingController : ControllerBase
{
    private readonly SiteSettingsService _settings;

    public BrandingController(SiteSettingsService settings) => _settings = settings;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await _settings.GetBrandingConfigAsync(ct));
}
