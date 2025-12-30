using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripShare.Api.Services;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Controllers;

public sealed record SuspendUserRequest(bool Suspend);

public sealed record HideTripRequest(bool Hide);

public sealed record DriverVerificationRequest(bool Verified, string? Note);

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public sealed class AdminController : ControllerBase
{
    private readonly AdminService _admin;
    private readonly SiteSettingsService _settings;
    private readonly AppDbContext _db;

    public AdminController(AdminService admin, SiteSettingsService settings, AppDbContext db)
    {
        _admin = admin;
        _settings = settings;
        _db = db;
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> Metrics(CancellationToken ct) => Ok(await _admin.GetMetricsAsync(ct));

    [HttpPost("users/{userId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid userId, [FromBody] SuspendUserRequest req, CancellationToken ct)
    {
        await _admin.SuspendUserAsync(userId, req.Suspend, ct);
        return NoContent();
    }

    [HttpPost("trips/{tripId:guid}/hide")]
    public async Task<IActionResult> HideTrip(Guid tripId, [FromBody] HideTripRequest req, CancellationToken ct)
    {
        await _admin.HideTripAsync(tripId, req.Hide, ct);
        return NoContent();
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        var driverVerificationRequired = await _settings.GetDriverVerificationRequiredAsync(ct);
        return Ok(new { driverVerificationRequired });
    }

    [HttpPost("settings/driver-verification")]
    public async Task<IActionResult> SetDriverVerification([FromBody] bool required, CancellationToken ct)
    {
        await _settings.SetDriverVerificationRequiredAsync(required, ct);
        return NoContent();
    }

    [HttpPost("users/{userId:guid}/driver-verify")]
    public async Task<IActionResult> VerifyDriver(Guid userId, [FromBody] DriverVerificationRequest req, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return NotFound();

        user.DriverVerified = req.Verified;
        user.DriverVerifiedAt = req.Verified ? DateTimeOffset.UtcNow : null;
        user.DriverVerificationNote = req.Note;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
