using Radar.Data;

namespace Radar.Collector.Entur;
public interface IEnturLineClient
{
    Task<IReadOnlyList<Line>> GetLinesAsync(CancellationToken ct);

}