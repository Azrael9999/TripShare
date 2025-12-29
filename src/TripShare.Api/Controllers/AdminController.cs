using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripShare.Api.Services;

namespace TripShare.Api.Controllers;

public sealed record SuspendUserRequest(bool Suspend);

public sealed record HideTripRequest(bool Hide);

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public sealed class AdminController : ControllerBase
{
    private readonly AdminService _admin;
    public AdminController(AdminService admin) => _admin = admin;

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
}
