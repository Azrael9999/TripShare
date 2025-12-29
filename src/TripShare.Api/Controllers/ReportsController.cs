using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripShare.Api.Helpers;
using TripShare.Api.Services;
using TripShare.Domain.Entities;

namespace TripShare.Api.Controllers;

public sealed record CreateReportRequest(ReportTargetType TargetType, Guid? TargetUserId, Guid? TripId, Guid? BookingId, string Reason, string? Details);

public sealed record UpdateReportRequest(ReportStatus Status, string? AdminNote);

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly ReportService _reports;

    public ReportsController(ReportService reports) => _reports = reports;

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReportRequest req, CancellationToken ct)
    {
        var r = new Report
        {
            ReporterUserId = User.GetUserId(),
            TargetType = req.TargetType,
            TargetUserId = req.TargetUserId,
            TripId = req.TripId,
            BookingId = req.BookingId,
            Reason = req.Reason,
            Details = req.Details
        };
        return Ok(await _reports.CreateAsync(r, ct));
    }

    [Authorize(Roles = "admin")]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ReportStatus? status = null, [FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(await _reports.ListAsync(status, take, ct));

    [Authorize(Roles = "admin")]
    [HttpPost("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateReportRequest req, CancellationToken ct)
    {
        await _reports.UpdateStatusAsync(id, req.Status, req.AdminNote, ct);
        return NoContent();
    }
}
