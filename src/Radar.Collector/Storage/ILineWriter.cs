using Radar.Data;

namespace Radar.Collector.Storage;

public interface ILineWriter
{
    Task<int> UpsertLinesAsync(IReadOnlyList<Line> lines, CancellationToken ct);
}