using Radar.Data;

namespace Radar.Collector.Entur;
public record ParseResult
(
    List<StopEvent> Events,
    int SkippedTrips
);