using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TripShare.Api.Helpers;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/telemetry")]
public sealed class TelemetryController : ControllerBase
{
    private readonly ILogger<TelemetryController> _log;

    public TelemetryController(ILogger<TelemetryController> log) => _log = log;

    public sealed record ClientLogRequest(
        string Message,
        string? Severity,
        string? Stack,
        string? Route,
        string? Uri,
        IDictionary<string, string?>? Properties);

    [HttpPost("logs")]
    [AllowAnonymous]
    [EnableRateLimiting("telemetry")]
    public IActionResult Log([FromBody] ClientLogRequest req)
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        var correlationId = HttpContext.GetCorrelationId();
        var severity = (req.Severity ?? "info").ToLowerInvariant();

        var state = new Dictionary<string, object?>
        {
            ["route"] = req.Route,
            ["uri"] = req.Uri,
            ["userAgent"] = userAgent,
            ["correlationId"] = correlationId
        };

        if (req.Properties is not null)
        {
            foreach (var kvp in req.Properties)
            {
                state[$"prop_{kvp.Key}"] = kvp.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(req.Stack))
        {
            state["stack"] = req.Stack;
        }

        using var scope = _log.BeginScope(state);

        switch (severity)
        {
            case "error":
                _log.LogError("{Message}", req.Message);
                break;
            case "warn":
            case "warning":
                _log.LogWarning("{Message}", req.Message);
                break;
            case "verbose":
            case "debug":
                _log.LogDebug("{Message}", req.Message);
                break;
            default:
                _log.LogInformation("{Message}", req.Message);
                break;
        }

        _log.LogTrace("Client log ingested");

        return Accepted();
    }
}
