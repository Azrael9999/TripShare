using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TripShare.Api.Filters;
using TripShare.Api.Helpers;
using TripShare.Api.Services;
using TripShare.Application.Contracts;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/trips")]
public sealed class TripsController : ControllerBase
{
    private readonly TripService _trips;

    public TripsController(TripService trips) => _trips = trips;

    [HttpPost("search")]
    public async Task<ActionResult<PagedResult<TripSummaryDto>>> Search(
        [FromBody] SearchTripsRequest req,
        CancellationToken ct)
        => Ok(await _trips.SearchAsync(req, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TripSummaryDto>> Get(Guid id, CancellationToken ct)
    {
        Guid? requester = User.Identity?.IsAuthenticated == true ? User.GetUserId() : null;
        var trip = await _trips.GetByIdAsync(id, requester, ct);
        return trip is null ? NotFound() : Ok(trip);
    }

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<TripSummaryDto>>> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _trips.MyTripsAsync(User.GetUserId(), page, pageSize, ct));

    [Authorize]
    [RequireVerifiedEmail]
    [IdempotencyKey]
    [HttpPost]
    public async Task<ActionResult<TripSummaryDto>> Create(
        [FromBody] CreateTripRequest req,
        CancellationToken ct)
        => Ok(await _trips.CreateAsync(User.GetUserId(), req, ct));

    [Authorize]
    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        await _trips.StartTripAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await _trips.CompleteTripAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    public sealed record CancelTripRequest(string? Reason);

    [Authorize]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelTripRequest req,
        CancellationToken ct)
    {
        await _trips.CancelTripAsync(User.GetUserId(), id, req.Reason, ct);
        return NoContent();
    }

    public sealed record VisibilityRequest(bool IsPublic);

    [Authorize]
    [HttpPost("{id:guid}/visibility")]
    public async Task<IActionResult> Visibility(
        Guid id,
        [FromBody] VisibilityRequest req,
        CancellationToken ct)
    {
        await _trips.SetVisibilityAsync(User.GetUserId(), id, req.IsPublic, ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateTripStatusRequest req,
        CancellationToken ct)
    {
        await _trips.SetStatusAsync(User.GetUserId(), id, req, ct);
        return NoContent();
    }

    [Authorize]
    [EnableRateLimiting("location-updates")]
    [HttpPost("{id:guid}/location")]
    public async Task<IActionResult> UpdateLocation(
        Guid id,
        [FromBody] UpdateTripLocationRequest req,
        CancellationToken ct)
    {
        await _trips.UpdateLocationAsync(User.GetUserId(), id, req, ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("{id:guid}/eta")]
    public async Task<ActionResult<TripEtaResponse>> Eta(Guid id, CancellationToken ct)
    {
        return Ok(await _trips.CalculateEtasAsync(User.GetUserId(), id, ct));
    }

    [Authorize]
    [HttpGet("{id:guid}/location/stream")]
    public async Task StreamLocation(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Add("X-Accel-Buffering", "no");
        Response.ContentType = "text/event-stream";

        await foreach (var update in _trips.StreamLocationsAsync(User.GetUserId(), id, ct))
        {
            await Response.WriteAsync($"event: location\n", ct);
            await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(update)}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }
}
