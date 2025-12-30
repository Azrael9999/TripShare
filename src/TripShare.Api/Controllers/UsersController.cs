using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TripShare.Api.Helpers;
using TripShare.Api.Services;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly RatingService _ratings;

    public UsersController(AppDbContext db, AuthService auth, RatingService ratings)
    {
        _db = db;
        _auth = auth;
        _ratings = ratings;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<object>> Me(CancellationToken ct)
    {
        var id = User.GetUserId();
        var user = await _db.Users.AsNoTracking().Include(x => x.Vehicle).SingleAsync(x => x.Id == id, ct);
        var avg = await _ratings.GetUserAverageAsync(id, ct);

        return Ok(new
        {
            user.Id,
            user.Email,
            user.EmailVerified,
            user.DisplayName,
            user.PhotoUrl,
            user.IsDriver,
            user.DriverVerified,
            user.Role,
            driverVerifiedAt = user.DriverVerifiedAt,
            driverVerificationNote = user.DriverVerificationNote,
            ratingAverage = avg,
            vehicle = user.Vehicle == null ? null : new { user.Vehicle.Make, user.Vehicle.Model, user.Vehicle.Color, user.Vehicle.PlateNumber, user.Vehicle.Seats }
        });
    }

    [Authorize]
    [HttpPost("me/resend-verification")]
    public async Task<IActionResult> ResendVerification(CancellationToken ct)
    {
        await _auth.ResendVerificationAsync(User.GetUserId(), ct);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token, CancellationToken ct)
    {
        await _auth.VerifyEmailAsync(token, ct);
        return NoContent();
    }


public sealed record UpsertVehicleRequest(string Make, string Model, string Color, string? PlateNumber, int Seats);

    [Authorize]
    [HttpGet("me/vehicle")]
    public async Task<IActionResult> MyVehicle(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var v = await _db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.OwnerUserId == userId, ct);
        if (v == null) return Ok(null);
        return Ok(new { v.Make, v.Model, v.Color, v.PlateNumber, v.Seats });
    }

    [Authorize]
    [HttpPut("me/vehicle")]
    public async Task<IActionResult> UpsertVehicle([FromBody] UpsertVehicleRequest req, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var v = await _db.Vehicles.FirstOrDefaultAsync(x => x.OwnerUserId == userId, ct);
        if (v == null)
        {
            v = new TripShare.Domain.Entities.Vehicle
            {
                OwnerUserId = userId,
                Make = req.Make,
                Model = req.Model,
                Color = req.Color,
                PlateNumber = req.PlateNumber,
                Seats = Math.Clamp(req.Seats, 1, 12),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.Vehicles.Add(v);
        }
        else
        {
            v.Make = req.Make;
            v.Model = req.Model;
            v.Color = req.Color;
            v.PlateNumber = req.PlateNumber;
            v.Seats = Math.Clamp(req.Seats, 1, 12);
            v.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // Mark user as driver if they added vehicle
        var user = await _db.Users.FirstAsync(x => x.Id == userId, ct);
        user.IsDriver = true;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

}
