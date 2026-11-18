
namespace Radar.Api.Repositories;

public record HourlyDelay(
    int Hour,
    int Stops,
    int MedianS
);
