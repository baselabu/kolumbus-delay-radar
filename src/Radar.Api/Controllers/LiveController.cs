using Microsoft.AspNetCore.Mvc;
using Radar.Api.DTOs;
using Radar.Api.Services;
using Microsoft.AspNetCore.OutputCaching;
namespace Radar.Api.Controllers;

[ApiController]
[Route("api/live")]
[OutputCache(PolicyName = "live")]

public class LiveController(ILiveDelayService service) : ControllerBase
{
    /// <summary>Buses that are late right now (most recent stop passed within the last 15 minutes).</summary>
    [HttpGet("late")]
    public async Task<ActionResult<IReadOnlyList<LateBusDto>>> GetLate(
        [FromQuery] int minDelay = DelayRules.OnTimeLateLimitMinutes,
        CancellationToken ct = default)
    {
        if (minDelay < 0)
        {
            return Problem(
                statusCode: 400,
                title: "Invalid minimum delay",
                detail: "Minimum delay cannot be negative.");
        }

        var lateBuses = await service.GetLateBusesAsync(minDelay, ct);
        return Ok(lateBuses);
    }
}