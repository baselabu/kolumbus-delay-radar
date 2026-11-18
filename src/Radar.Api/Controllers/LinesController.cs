using Microsoft.AspNetCore.Mvc;
using Radar.Api.Repositories;
using Radar.Api.Services;
using Microsoft.AspNetCore.OutputCaching;

namespace Radar.Api.Controllers;

[ApiController]
[Route("api/lines")]
[OutputCache(PolicyName = "stats")]
public class LinesController(IStatisticsService service) : ControllerBase
{
    /// <summary>Get punctuality for a line over the last N days.</summary>
    [HttpGet("{line}/punctuality")]
    public async Task<ActionResult<LinePunctuality>> GetLinePunctuality(
        string line,
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

        var punctuality = await service.GetLinePunctualityAsync(line, days, ct);
        if (punctuality is null)
        {
            return Problem(
                statusCode: 404,
                title: "No data for line",
                detail: $"No data for line '{line}' in the last {days} days.");
        }
        return Ok(punctuality);
    }

    /// <summary>Lines ranked from least to most punctual over the last N days (period "all" or weekday "rush" hours);
    /// only lines with at least 50 stops. Each line has its on-time share, median and p90 delay.</summary>

    [HttpGet("ranking")]
    public async Task<ActionResult<IReadOnlyList<LinePunctuality>>> GetRanking(
        [FromQuery] string period = "all",
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
        if (period != "rush" && period != "all")
        {
            return Problem(
                statusCode: 400,
                title: "Invalid period parameter",
                detail: "Period must be either 'rush' or 'all'.");
        }
        bool rush = period == "rush";

        var ranking = await service.GetRankingAsync(rush, days, ct);
        return Ok(ranking);
    }

}
