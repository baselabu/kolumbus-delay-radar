using Microsoft.AspNetCore.Mvc;
using Radar.Api.Repositories;
using Radar.Api.Services;
using Microsoft.AspNetCore.OutputCaching;

namespace Radar.Api.Controllers;

[ApiController]
[Route("api/stops")]
[OutputCache(PolicyName = "stats")]
public class StopsController(IStatisticsService service) : ControllerBase
{
    /// <summary>Median delay per hour of the day (Oslo time) at a stop over the last N days; only hours with departures.</summary>
    /// <summary>
    /// All stops (platforms) that have data: stop reference and name. Used by the demo UI's stop search.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StopInfo>>> GetStops(CancellationToken ct = default)
        => Ok(await service.GetStopsAsync(ct));

    [HttpGet("{stopRef}/delays")]
    public async Task<ActionResult<IReadOnlyList<HourlyDelay>>> GetStopDelaysByHour(
        string stopRef,
        [FromQuery] int days = 7,
        CancellationToken ct = default)
    {
        if (!DelayRules.IsValidDays(days))
        {
            return Problem(
                statusCode: 400,
                title: "Invalid days parameter",
                detail: $"Days must be between 1 and {DelayRules.MaxDays}.");
        }

        var delays = await service.GetStopDelaysByHourAsync(stopRef, days, ct);

        if (delays.Count == 0)
        {
            return Problem(
                statusCode: 404,
                title: "No delay data for stop",
                detail: $"No delay data for stop '{stopRef}' in the last {days} days.");
        }
        return Ok(delays);
    }

}