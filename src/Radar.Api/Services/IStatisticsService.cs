using Radar.Api.Repositories;

namespace Radar.Api.Services;

public interface IStatisticsService
{
    Task<LinePunctuality?> GetLinePunctualityAsync(string line, int days, CancellationToken ct);
    Task<IReadOnlyList<LinePunctuality>> GetRankingAsync(bool rush, int days, CancellationToken ct);
    Task<IReadOnlyList<HourlyDelay>> GetStopDelaysByHourAsync(string stopRef, int days, CancellationToken ct);
    Task<IReadOnlyList<StopInfo>> GetStopsAsync(CancellationToken ct);
}