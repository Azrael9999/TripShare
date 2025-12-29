using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripShare.Api.Helpers;
using TripShare.Api.Services;

namespace TripShare.Api.Controllers;

[ApiController]
[Route("api/blocks")]
[Authorize]
public sealed class BlocksController : ControllerBase
{
    private readonly BlockService _blocks;
    public BlocksController(BlockService blocks) => _blocks = blocks;

    [HttpPost("{blockedUserId:guid}")]
    public async Task<IActionResult> Block(Guid blockedUserId, CancellationToken ct)
    {
        await _blocks.BlockAsync(User.GetUserId(), blockedUserId, ct);
        return NoContent();
    }

    [HttpDelete("{blockedUserId:guid}")]
    public async Task<IActionResult> Unblock(Guid blockedUserId, CancellationToken ct)
    {
        await _blocks.UnblockAsync(User.GetUserId(), blockedUserId, ct);
        return NoContent();
    }


    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _blocks.ListAsync(User.GetUserId(), ct));

}
