using Radar.Api.Repositories;
using Radar.Api.Services;

namespace Radar.Api.Services;

public class StatisticsService(IStatisticsRepository repo, TimeProvider time) : IStatisticsService
{
    public async Task<LinePunctuality?> GetLinePunctualityAsync(string line, int days, CancellationToken ct)
    {
        var start = time.GetUtcNow().AddDays(-days);
        var result = await repo.GetLinePunctualityAsync(line, start, ct);
        return result;
    }

    public async Task<IReadOnlyList<LinePunctuality>> GetRankingAsync(bool rush, int days, CancellationToken ct)
    {
        var start = time.GetUtcNow().AddDays(-days);
        var result = await repo.GetRankingAsync(start, rush, DelayRules.RankingMinStops, ct);
        return result;
    }

    public async Task<IReadOnlyList<HourlyDelay>> GetStopDelaysByHourAsync(string stopRef, int days, CancellationToken ct)
    {
        var start = time.GetUtcNow().AddDays(-days);
        var result = await repo.GetStopDelaysByHourAsync(stopRef, start, ct);
        return result;
    }

    public Task<IReadOnlyList<StopInfo>> GetStopsAsync(CancellationToken ct) => repo.GetStopsAsync(ct);

}