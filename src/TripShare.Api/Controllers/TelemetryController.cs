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
    private readonly IConfiguration _cfg;

    public TelemetryController(ILogger<TelemetryController> log, IConfiguration cfg)
    {
        _log = log;
        _cfg = cfg;
    }

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
        var apiKey = _cfg["Telemetry:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var provided = Request.Headers["X-Telemetry-Key"].FirstOrDefault();
            if (!string.Equals(apiKey, provided, StringComparison.Ordinal))
            {
                return Unauthorized();
            }
        }

        var sampleRate = Math.Clamp(_cfg.GetValue<double?>("Telemetry:SampleRate") ?? 1.0, 0, 1);
        if (sampleRate < 1 && Random.Shared.NextDouble() > sampleRate)
        {
            return Accepted();
        }

        var userAgent = Request.Headers.UserAgent.ToString();
        var correlationId = HttpContext.GetCorrelationId();
        var severity = (req.Severity ?? "info").ToLowerInvariant();

        var message = req.Message.Length > 1024 ? req.Message[..1024] : req.Message;
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
                _log.LogError("{Message}", message);
                break;
            case "warn":
            case "warning":
                _log.LogWarning("{Message}", message);
                break;
            case "verbose":
            case "debug":
                _log.LogDebug("{Message}", message);
                break;
            default:
                _log.LogInformation("{Message}", message);
                break;
        }

        _log.LogTrace("Client log ingested");

        return Accepted();
    }
}
