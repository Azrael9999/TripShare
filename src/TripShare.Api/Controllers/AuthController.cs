using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TripShare.Application.Contracts;
using TripShare.Api.Services;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    [HttpPost("google")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Google([FromBody] GoogleLoginRequest req, CancellationToken ct)
        => Ok(await _auth.GoogleLoginAsync(req, ct));

    [HttpPost("password/register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> RegisterWithPassword([FromBody] PasswordRegisterRequest req, CancellationToken ct)
        => Ok(await _auth.PasswordRegisterAsync(req, ct));

    [HttpPost("password/login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> PasswordLogin([FromBody] PasswordLoginRequest req, CancellationToken ct)
        => Ok(await _auth.PasswordLoginAsync(req, ct));

    [HttpPost("password/reset/request")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult> RequestPasswordReset([FromBody] PasswordResetRequest req, CancellationToken ct)
    {
        await _auth.RequestPasswordResetAsync(req, ct);
        return Accepted();
    }

    [HttpPost("password/reset/confirm")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult> ConfirmPasswordReset([FromBody] PasswordResetConfirm req, CancellationToken ct)
    {
        await _auth.ConfirmPasswordResetAsync(req, ct);
        return NoContent();
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
        => Ok(await _auth.RefreshAsync(req, ct));

    [HttpPost("sms/request")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult> RequestSmsOtp([FromBody] SmsOtpRequest req, CancellationToken ct)
    {
        await _auth.RequestSmsOtpAsync(req, ct);
        return Accepted();
    }

    [HttpPost("sms/verify")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthResponse>> VerifySmsOtp([FromBody] SmsOtpVerifyRequest req, CancellationToken ct)
        => Ok(await _auth.VerifySmsOtpAsync(req, ct));
}
