using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripShare.Api.Services;
using TripShare.Application.Contracts;
using TripShare.Domain.Entities;
using TripShare.Api.Helpers;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Controllers;

public sealed record SuspendUserRequest(bool Suspend, string? Note);

public sealed record HideTripRequest(bool Hide, string? Note);

public sealed record DriverVerificationRequest(bool Verified, string? Note);

public sealed record ResolveIncidentRequest(SafetyIncidentStatus Status, string? Note);

public sealed record AdminUpsertRequest(string Email, string? DisplayName, string? Password, bool Approved);

public sealed record DriverSearchResult(Guid Id, string? DisplayName, string? Email, string? PhoneNumber, string? VehicleSummary);

public sealed record DriverVerificationGateRequest(bool Required);

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin,superadmin")]
public sealed class AdminController : ControllerBase
{
    private readonly AdminService _admin;
    private readonly SiteSettingsService _settings;
    private readonly AppDbContext _db;
    private readonly SafetyService _safety;
    private readonly IdentityVerificationService _identity;
    private readonly ILogger<AdminController> _log;

    public AdminController(AdminService admin, SiteSettingsService settings, AppDbContext db, SafetyService safety, IdentityVerificationService identity, ILogger<AdminController> log)
    {
        _admin = admin;
        _settings = settings;
        _db = db;
        _safety = safety;
        _identity = identity;
        _log = log;
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> Metrics(CancellationToken ct) => Ok(await _admin.GetMetricsAsync(ct));

    [HttpPost("users/{userId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid userId, [FromBody] SuspendUserRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Note))
            return BadRequest(new { message = "A suspension note is required." });

        await _admin.SuspendUserAsync(userId, req.Suspend, req.Note.Trim(), ct);
        return NoContent();
    }

    [HttpPost("trips/{tripId:guid}/hide")]
    public async Task<IActionResult> HideTrip(Guid tripId, [FromBody] HideTripRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Note))
            return BadRequest(new { message = "A trip visibility note is required." });

        await _admin.HideTripAsync(tripId, req.Hide, req.Note.Trim(), ct);
        return NoContent();
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        var driverVerificationRequired = await _settings.GetDriverVerificationRequiredAsync(ct);
        AdConfigurationDto? ads = null;
        if (User.IsSuperAdmin())
        {
            ads = await _settings.GetAdConfigurationAsync(ct);
        }
        return Ok(new
        {
            driverVerificationRequired,
            ads = ads is null
                ? null
                : new
                {
                    ads.Enabled,
                    ads.FrequencyCapPerSession,
                    ads.MaxSlotsPerPage,
                    slots = ads.Slots
                }
        });
    }

    [HttpPost("settings/driver-verification")]
    [Authorize(Roles = "superadmin")]
    public async Task<IActionResult> SetDriverVerification([FromBody] DriverVerificationGateRequest req, CancellationToken ct)
    {
        try
        {
            await _settings.SetDriverVerificationRequiredAsync(req.Required, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to update driver verification requirement to {Required}.", req.Required);
            return BadRequest(new { message = "Failed to update setting." });
        }
    }

    [HttpGet("ads/config")]
    [Authorize(Roles = "superadmin")]
    public async Task<IActionResult> GetAdConfiguration(CancellationToken ct)
        => Ok(await _settings.GetAdConfigurationAsync(ct));

    [HttpPost("ads/config")]
    [Authorize(Roles = "superadmin")]
    public async Task<IActionResult> SetAdConfiguration([FromBody] AdConfigurationDto config, CancellationToken ct)
    {
        await _settings.SetAdConfigurationAsync(config, ct);
        return NoContent();
    }

    [HttpGet("branding")]
    [Authorize(Roles = "superadmin")]
    public async Task<IActionResult> GetBranding(CancellationToken ct)
        => Ok(await _settings.GetBrandingConfigAsync(ct));

    [HttpPost("branding")]
    [Authorize(Roles = "superadmin")]
    public async Task<IActionResult> SetBranding([FromBody] BrandingConfigDto config, CancellationToken ct)
    {
        await _settings.SetBrandingConfigAsync(config, ct);
        return NoContent();
    }

    [HttpGet("maps/config")]
    [Authorize(Roles = "superadmin")]
    public async Task<ActionResult<MapApiConfigDto>> GetMapsConfig(CancellationToken ct)
        => Ok(await _settings.GetMapApiConfigAsync(ct));

    [HttpPost("maps/config")]
    [Authorize(Roles = "superadmin")]
    public async Task<IActionResult> SetMapsConfig([FromBody] MapApiConfigDto config, CancellationToken ct)
    {
        await _settings.SetMapApiConfigAsync(config, ct);
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

    [HttpGet("identity-verifications")]
    public async Task<IActionResult> IdentityVerifications([FromQuery] IdentityVerificationStatus? status = null, [FromQuery] int take = 100, CancellationToken ct = default)
        => Ok((await _identity.ListAsync(status, take, ct)).Select(ToDto));

    [HttpGet("drivers/search")]
    public async Task<IActionResult> SearchDrivers([FromQuery] string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3)
            return Ok(Array.Empty<DriverSearchResult>());

        return Ok(await _admin.SearchDriversAsync(query.Trim(), ct));
    }

    [HttpPost("identity-verifications/{id:guid}/review")]
    public async Task<IActionResult> ReviewIdentityVerification(Guid id, [FromBody] IdentityVerificationReview review, CancellationToken ct)
    {
        await _identity.ReviewAsync(id, User.GetUserId(), review, ct);
        return NoContent();
    }

    [HttpGet("safety-incidents")]
    public async Task<IActionResult> SafetyIncidents([FromQuery] SafetyIncidentStatus? status = null, [FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(await _safety.ListIncidentsAsync(status, take, ct));

    [HttpPost("safety-incidents/{id:guid}/resolve")]
    public async Task<IActionResult> ResolveIncident(Guid id, [FromBody] ResolveIncidentRequest req, CancellationToken ct)
    {
        await _safety.ResolveIncidentAsync(id, User.GetUserId(), req.Status, req.Note, ct);
        return NoContent();
    }

    [HttpGet("admins")]
    [Authorize(Roles = "superadmin")]
    public async Task<IActionResult> ListAdmins(CancellationToken ct)
        => Ok(await _admin.ListAdminsAsync(ct));

    [HttpPost("admins")]
    [Authorize(Roles = "superadmin")]
    public async Task<IActionResult> UpsertAdmin([FromBody] AdminUpsertRequest req, CancellationToken ct)
    {
        await _admin.UpsertAdminAsync(req, ct);
        return NoContent();
    }

    private static IdentityVerificationDto ToDto(IdentityVerificationRequest req)
        => new(req.Id, req.Status, req.DocumentType, req.DocumentReference, req.SubmittedAt, req.ReviewedAt, req.ReviewerNote, req.KycProvider, req.KycReference);
}
