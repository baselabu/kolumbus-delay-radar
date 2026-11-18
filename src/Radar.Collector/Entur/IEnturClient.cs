
namespace Radar.Collector.Entur;
public interface IEnturClient
{
    Task<Stream> GetEstimatedTimetableAsync(CancellationToken ct);
}