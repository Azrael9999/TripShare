using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripShare.Api.Services;
using TripShare.Application.Contracts;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Controllers;

public sealed record SuspendUserRequest(bool Suspend);

public sealed record HideTripRequest(bool Hide);

public sealed record DriverVerificationRequest(bool Verified, string? Note);

public sealed record ResolveIncidentRequest(SafetyIncidentStatus Status, string? Note);

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public sealed class AdminController : ControllerBase
{
    private readonly AdminService _admin;
    private readonly SiteSettingsService _settings;
    private readonly AppDbContext _db;
    private readonly SafetyService _safety;
    private readonly IdentityVerificationService _identity;

    public AdminController(AdminService admin, SiteSettingsService settings, AppDbContext db, SafetyService safety, IdentityVerificationService identity)
    {
        _admin = admin;
        _settings = settings;
        _db = db;
        _safety = safety;
        _identity = identity;
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

    [HttpGet("identity-verifications")]
    public async Task<IActionResult> IdentityVerifications([FromQuery] IdentityVerificationStatus? status = null, [FromQuery] int take = 100, CancellationToken ct = default)
        => Ok((await _identity.ListAsync(status, take, ct)).Select(ToDto));

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

    private static IdentityVerificationDto ToDto(IdentityVerificationRequest req)
        => new(req.Id, req.Status, req.DocumentType, req.DocumentReference, req.SubmittedAt, req.ReviewedAt, req.ReviewerNote, req.KycProvider, req.KycReference);
}
