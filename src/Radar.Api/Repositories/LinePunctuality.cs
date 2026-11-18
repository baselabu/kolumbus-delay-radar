
namespace Radar.Api.Repositories;

public record LinePunctuality(
    string Line,
    int Stops,
    decimal OnTimePct,
    int MedianS,
    int P90S
);
