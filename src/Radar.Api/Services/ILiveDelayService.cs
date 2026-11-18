using Radar.Api.DTOs;

namespace Radar.Api.Services;

public interface ILiveDelayService
{
    Task<IReadOnlyList<LateBusDto>> GetLateBusesAsync(int minDelayMinutes, CancellationToken ct);

}