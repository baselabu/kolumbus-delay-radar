
namespace Radar.Api.DTOs;

public record LateBusDto(
    string Line,
    string? StopName,
    DateTimeOffset Aimed,
    DateTimeOffset Actual,
    int DelaySeconds,
    string JourneyRef);
