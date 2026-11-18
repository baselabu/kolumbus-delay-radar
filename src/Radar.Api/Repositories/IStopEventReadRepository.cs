
namespace Radar.Api.Repositories;

public interface IStopEventReadRepository
{
    Task<IReadOnlyList<RecentStop>> GetStopsPassedSinceAsync(DateTimeOffset since, CancellationToken ct);
}