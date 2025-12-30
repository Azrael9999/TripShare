using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripShare.Api.Filters;
using TripShare.Api.Helpers;
using TripShare.Api.Services;
using TripShare.Application.Contracts;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public sealed class BookingsController : ControllerBase
{
    private readonly BookingService _bookings;

    public BookingsController(BookingService bookings) => _bookings = bookings;

    [Authorize]
    [HttpGet("mine")]
    public async Task<ActionResult<List<BookingDto>>> Mine(CancellationToken ct)
        => Ok(await _bookings.MyBookingsAsync(User.GetUserId(), ct));

    [Authorize]
    [HttpGet("driver")]
    public async Task<ActionResult<List<BookingDto>>> Driver(CancellationToken ct)
        => Ok(await _bookings.DriverBookingsAsync(User.GetUserId(), ct));

    [Authorize]
    [RequireVerifiedEmail]
    [IdempotencyKey]
    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create([FromBody] CreateBookingRequest req, CancellationToken ct)
        => Ok(await _bookings.CreateAsync(User.GetUserId(), req, ct));

    [Authorize]
    [HttpPost("{id:guid}/status/passenger")]
    public async Task<IActionResult> PassengerSetStatus(Guid id, [FromBody] SetBookingStatusRequest req, CancellationToken ct)
    {
        await _bookings.SetStatusAsync(User.GetUserId(), id, req, isDriverAction: false, ct);
        return NoContent();
    }


    [Authorize]
    [HttpGet("{id:guid}/contact")]
    public async Task<IActionResult> Contact(Guid id, CancellationToken ct)
        => Ok(await _bookings.RevealContactAsync(User.GetUserId(), id, ct));


    [Authorize]
    [HttpPost("{id:guid}/status/driver")]
    public async Task<IActionResult> DriverSetStatus(Guid id, [FromBody] SetBookingStatusRequest req, CancellationToken ct)
    {
        await _bookings.SetStatusAsync(User.GetUserId(), id, req, isDriverAction: true, ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("{id:guid}/progress")]
    public async Task<IActionResult> UpdateProgress(Guid id, [FromBody] UpdateBookingProgressRequest req, CancellationToken ct)
    {
        await _bookings.UpdateProgressAsync(User.GetUserId(), id, req, ct);
        return NoContent();
    }
}
