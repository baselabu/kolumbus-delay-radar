
namespace Radar.Data;

public record StopEvent
(
    DateOnly OperatingDate,
    string JourneyRef,
    int StopOrder,
    string LineRef,
    string StopPointRef,
    string? StopPointName,
    DateTimeOffset? AimedArrivalTime,
    DateTimeOffset? ExpectedArrivalTime,
    DateTimeOffset? ActualArrivalTime,
    DateTimeOffset? AimedDepartureTime,
    DateTimeOffset? ExpectedDepartureTime,
    DateTimeOffset? ActualDepartureTime,
    bool Cancelled

);

