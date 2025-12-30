using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripShare.Api.Helpers;
using TripShare.Api.Services;
using TripShare.Application.Contracts;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/safety")]
public sealed class SafetyController : ControllerBase
{
    private readonly SafetyService _safety;

    public SafetyController(SafetyService safety)
    {
        _safety = safety;
    }

    [Authorize]
    [HttpGet("emergency-contact")]
    public async Task<ActionResult<EmergencyContactResponse?>> GetEmergencyContact(CancellationToken ct)
    {
        var contact = await _safety.GetEmergencyContactAsync(User.GetUserId(), ct);
        if (contact is null) return Ok(null);
        return Ok(new EmergencyContactResponse(contact.Id, contact.Name, contact.PhoneNumber, contact.Email, contact.ShareLiveTripsByDefault));
    }

    [Authorize]
    [HttpPut("emergency-contact")]
    public async Task<ActionResult<EmergencyContactResponse>> UpsertEmergencyContact([FromBody] EmergencyContactRequest req, CancellationToken ct)
    {
        var contact = await _safety.UpsertEmergencyContactAsync(User.GetUserId(), req, ct);
        return Ok(new EmergencyContactResponse(contact.Id, contact.Name, contact.PhoneNumber, contact.Email, contact.ShareLiveTripsByDefault));
    }

    [Authorize]
    [HttpDelete("emergency-contact")]
    public async Task<IActionResult> DeleteEmergencyContact(CancellationToken ct)
    {
        await _safety.RemoveEmergencyContactAsync(User.GetUserId(), ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("panic")]
    public async Task<IActionResult> Panic([FromBody] PanicRequest req, CancellationToken ct)
    {
        var incident = await _safety.CreateIncidentAsync(User.GetUserId(), req, ct);
        return Ok(new { incident.Id, incident.Status, incident.CreatedAt, incident.Summary });
    }

    [Authorize]
    [HttpPost("share-trip")]
    public async Task<ActionResult<ShareLinkResponse>> ShareTrip([FromBody] CreateShareLinkRequest req, CancellationToken ct)
    {
        var link = await _safety.CreateShareLinkAsync(User.GetUserId(), req, ct);
        return Ok(new ShareLinkResponse(link.Token, link.ExpiresAt));
    }

    [AllowAnonymous]
    [HttpGet("share-trip/{tripId:guid}/{token}")]
    public async Task<IActionResult> GetShareTrip(Guid tripId, string token, CancellationToken ct)
    {
        var snapshot = await _safety.GetShareLinkSnapshotAsync(tripId, token, ct);
        return snapshot is null ? NotFound() : Ok(snapshot);
    }
}
