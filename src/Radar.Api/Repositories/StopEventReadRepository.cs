using Radar.Data;
using Microsoft.EntityFrameworkCore;

namespace Radar.Api.Repositories;

public class StopEventReadRepository(RadarDbContext db) : IStopEventReadRepository
{
    public async Task<IReadOnlyList<RecentStop>> GetStopsPassedSinceAsync(DateTimeOffset since, CancellationToken ct)
    {
        var query =
            from s in db.StopEvents
            join l in db.Lines on s.LineRef equals l.LineRef into lineGroup
            from l in lineGroup.DefaultIfEmpty()
            let actual = s.ActualDepartureTime ?? s.ActualArrivalTime
            let aimed = s.ActualDepartureTime != null ? s.AimedDepartureTime : s.AimedArrivalTime
            where
            actual != null && aimed != null && actual > since

            select new RecentStop(
                s.JourneyRef,
                l.PublicCode ?? s.LineRef,
                s.StopPointName,
                aimed.Value,
                actual.Value);

        return await query.ToListAsync(ct);
    }
}