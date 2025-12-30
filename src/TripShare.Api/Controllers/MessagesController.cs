using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripShare.Api.Helpers;
using TripShare.Api.Services;
using TripShare.Application.Contracts;
using TripShare.Domain.Entities;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public sealed class MessagesController : ControllerBase
{
    private readonly MessagingService _messaging;

    public MessagesController(MessagingService messaging) => _messaging = messaging;

    [HttpPost("threads")]
    public async Task<ActionResult<MessageThreadDto>> CreateThread([FromBody] CreateThreadRequest req, CancellationToken ct)
    {
        var thread = await _messaging.GetOrCreateBookingThreadAsync(req.BookingId, User.GetUserId(), ct);
        return Ok(ToDto(thread));
    }

    [HttpGet("threads")]
    public async Task<ActionResult<IEnumerable<MessageThreadDto>>> ListThreads(CancellationToken ct)
    {
        var threads = await _messaging.ListThreadsAsync(User.GetUserId(), ct);
        return Ok(threads.Select(ToDto));
    }

    [HttpGet("threads/{threadId:guid}/messages")]
    public async Task<ActionResult<IEnumerable<MessageDto>>> ListMessages(Guid threadId, [FromQuery] int take = 50, CancellationToken ct = default)
    {
        var messages = await _messaging.ListMessagesAsync(threadId, User.GetUserId(), take, ct);
        return Ok(messages.Select(ToDto));
    }

    [HttpPost("threads/{threadId:guid}/messages")]
    public async Task<ActionResult<MessageDto>> Send(Guid threadId, [FromBody] SendMessageRequest req, CancellationToken ct)
    {
        var msg = await _messaging.SendAsync(User.GetUserId(), threadId, req.Body, false, ct);
        return Ok(ToDto(msg));
    }

    private static MessageThreadDto ToDto(MessageThread t)
        => new(t.Id, t.TripId, t.BookingId, t.ParticipantAId, t.ParticipantBId, t.IsClosed, t.CreatedAt, t.UpdatedAt);

    private static MessageDto ToDto(Message m)
        => new(m.Id, m.ThreadId, m.SenderId, m.Body, m.IsSystem, m.SentAt);
}
