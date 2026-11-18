using Radar.Api.Repositories;
using Radar.Api.DTOs;

namespace Radar.Api.Services;

public class LiveDelayService(IStopEventReadRepository repo, TimeProvider time) : ILiveDelayService
{
    private static readonly TimeZoneInfo Oslo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");
public async Task<IReadOnlyList<LateBusDto>> GetLateBusesAsync(int minDelayMinutes, CancellationToken ct)
{
    var now = time.GetUtcNow();
    var stops = await repo.GetStopsPassedSinceAsync(now - DelayRules.LiveWindow, ct);

    return stops
        .GroupBy(s => s.JourneyRef)                         // one group per trip
        .Select(g => g.MaxBy(s => s.Actual)!)                // its most recent passed stop
        .Select(s => new LateBusDto(
            s.Line,
            s.StopName,
            TimeZoneInfo.ConvertTime(s.Aimed, Oslo),    // stored in UTC, shown in Norwegian time
            TimeZoneInfo.ConvertTime(s.Actual, Oslo),
            (int)(s.Actual - s.Aimed).TotalSeconds,     // a duration: the same in every time zone
            s.JourneyRef))
        .Where(b => b.DelaySeconds > minDelayMinutes * 60)   // only really late buses
        .OrderByDescending(b => b.DelaySeconds)              // most late first
        .ToList();
}
}
