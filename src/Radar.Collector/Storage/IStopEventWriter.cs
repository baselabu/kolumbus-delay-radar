using Radar.Data;
namespace Radar.Collector.Storage;
public interface IStopEventWriter
{

    Task<int> UpsertAsync(IReadOnlyList<StopEvent> events, CancellationToken ct);

}