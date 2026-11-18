
namespace Radar.Api.Repositories;

public interface IStatisticsRepository
{
    Task<LinePunctuality?> GetLinePunctualityAsync(string line, DateTimeOffset since, CancellationToken ct);
    Task<IReadOnlyList<LinePunctuality>> GetRankingAsync(DateTimeOffset since, bool rush, int minStops, CancellationToken ct);
    Task<IReadOnlyList<HourlyDelay>> GetStopDelaysByHourAsync(string stopRef, DateTimeOffset since, CancellationToken ct);
    Task<IReadOnlyList<StopInfo>> GetStopsAsync(CancellationToken ct);
}