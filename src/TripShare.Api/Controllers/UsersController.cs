using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TripShare.Api.Helpers;
using TripShare.Api.Services;
using TripShare.Application.Contracts;
using TripShare.Domain.Entities;
using TripShare.Infrastructure.Data;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuthService _auth;
    private readonly RatingService _ratings;
    private readonly IdentityVerificationService _identity;

    public UsersController(AppDbContext db, AuthService auth, RatingService ratings, IdentityVerificationService identity)
    {
        _db = db;
        _auth = auth;
        _ratings = ratings;
        _identity = identity;
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
            user.IdentityVerified,
            user.PhoneVerified,
            user.Role,
            driverVerifiedAt = user.DriverVerifiedAt,
            driverVerificationNote = user.DriverVerificationNote,
            identityVerifiedAt = user.IdentityVerifiedAt,
            phoneVerified = user.PhoneVerified,
            ratingAverage = avg,
            vehicle = user.Vehicle == null ? null : new { user.Vehicle.Make, user.Vehicle.Model, user.Vehicle.Color, user.Vehicle.PlateNumber, user.Vehicle.Seats }
        });
    }

    [Authorize]
    [EnableRateLimiting("verification")]
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

    [Authorize]
    [HttpGet("me/identity-verification")]
    public async Task<ActionResult<IdentityVerificationDto?>> IdentityVerificationStatus(CancellationToken ct)
    {
        var req = await _identity.GetLatestAsync(User.GetUserId(), ct);
        if (req is null) return Ok(null);
        return Ok(ToDto(req));
    }

    [Authorize]
    [HttpPost("me/identity-verification")]
    public async Task<ActionResult<IdentityVerificationDto>> SubmitIdentityVerification([FromBody] IdentityVerificationSubmission submission, CancellationToken ct)
    {
        var req = await _identity.SubmitAsync(User.GetUserId(), submission, ct);
        return Ok(ToDto(req));
    }

    [Authorize]
    [HttpPut("me/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        await _auth.UpdateProfileAsync(User.GetUserId(), req, ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me/export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var export = await _auth.ExportAccountAsync(User.GetUserId(), ct);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(export);
        return File(bytes, "application/json", $"hoptrip-account-{User.GetUserId()}.json");
    }

    [Authorize]
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe(CancellationToken ct)
    {
        await _auth.DeleteAccountAsync(User.GetUserId(), ct);
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

    private static IdentityVerificationDto ToDto(IdentityVerificationRequest req)
        => new(req.Id, req.Status, req.DocumentType, req.DocumentReference, req.SubmittedAt, req.ReviewedAt, req.ReviewerNote, req.KycProvider, req.KycReference);
}
