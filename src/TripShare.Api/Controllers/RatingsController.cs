using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripShare.Api.Helpers;
using TripShare.Api.Services;
using TripShare.Application.Contracts;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/ratings")]
public sealed class RatingsController : ControllerBase
{
    private readonly RatingService _ratings;

    public RatingsController(RatingService ratings) => _ratings = ratings;

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRatingRequest req, CancellationToken ct)
    {
        await _ratings.CreateAsync(User.GetUserId(), req, ct);
        return NoContent();
    }
}
