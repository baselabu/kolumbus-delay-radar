
namespace Radar.Api.Repositories;

public record RecentStop(
    string JourneyRef,
    string Line,
    string? StopName,
    DateTimeOffset Aimed,
    DateTimeOffset Actual);