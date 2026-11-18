using Microsoft.EntityFrameworkCore;
using Radar.Api.Services;
using Radar.Data;

namespace Radar.Api.Repositories;

public class StatisticsRepository(RadarDbContext db) : IStatisticsRepository
{
    public async Task<LinePunctuality?> GetLinePunctualityAsync(string line, DateTimeOffset since, CancellationToken ct)
    {
        return await db.Database.SqlQuery<LinePunctuality>($"""
        WITH delays AS (
        SELECT s.line_ref,
        extract(epoch FROM
        CASE WHEN s.actual_departure_time IS NOT NULL
        THEN s.actual_departure_time - s.aimed_departure_time
        ELSE s.actual_arrival_time - s.aimed_arrival_time END) AS d
        FROM stop_events s
        WHERE coalesce(s.actual_departure_time, s.actual_arrival_time) >= {since}
        )
        SELECT {line} AS line,
        count(*)::int AS stops,
        round(100.0 * count(*) FILTER (WHERE d BETWEEN {DelayRules.OnTimeEarlyLimitSeconds} AND {DelayRules.OnTimeLateLimitSeconds}) / count(*), 1) AS on_time_pct,
        round(percentile_cont(0.5) WITHIN GROUP (ORDER BY d)::numeric)::int AS median_s,
        round(percentile_cont(0.9) WITHIN GROUP (ORDER BY d)::numeric)::int AS p90s
        FROM delays d
        JOIN lines l ON l.line_ref = d.line_ref
        WHERE l.public_code = {line}
        HAVING count(*) > 0
        """).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<LinePunctuality>> GetRankingAsync(
        DateTimeOffset since, bool rush, int minStops, CancellationToken ct)
    {
        return await db.Database.SqlQuery<LinePunctuality>($"""
        WITH delays AS (
        SELECT s.line_ref,
        coalesce(s.actual_departure_time, s.actual_arrival_time) AS t,
        extract(epoch FROM
        CASE WHEN s.actual_departure_time IS NOT NULL
        THEN s.actual_departure_time - s.aimed_departure_time
        ELSE s.actual_arrival_time - s.aimed_arrival_time END) AS d
        FROM stop_events s
        WHERE coalesce(s.actual_departure_time, s.actual_arrival_time) >= {since}
        )
        SELECT coalesce(l.public_code, d.line_ref) AS line,
        count(*)::int AS stops,
        round(100.0 * count(*) FILTER (WHERE d BETWEEN {DelayRules.OnTimeEarlyLimitSeconds} AND {DelayRules.OnTimeLateLimitSeconds}) / count(*), 1) AS on_time_pct,
        round(percentile_cont(0.5) WITHIN GROUP (ORDER BY d)::numeric)::int AS median_s,
        round(percentile_cont(0.9) WITHIN GROUP (ORDER BY d)::numeric)::int AS p90s
        FROM delays d
        LEFT JOIN lines l ON l.line_ref = d.line_ref
        WHERE {rush} = false
        OR (extract(isodow FROM t AT TIME ZONE 'Europe/Oslo') BETWEEN 1 AND 5
        AND extract(hour FROM t AT TIME ZONE 'Europe/Oslo') IN (7, 8, 15, 16, 17))
        GROUP BY 1
        HAVING count(*) >= {minStops}
        ORDER BY on_time_pct, median_s DESC
        """).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<HourlyDelay>> GetStopDelaysByHourAsync(string stopRef, DateTimeOffset since, CancellationToken ct)
    {
        return await db.Database.SqlQuery<HourlyDelay>($"""
        WITH delays AS (
        SELECT coalesce(s.actual_departure_time, s.actual_arrival_time) AS t,
        extract(epoch FROM
        CASE WHEN s.actual_departure_time IS NOT NULL
        THEN s.actual_departure_time - s.aimed_departure_time
        ELSE s.actual_arrival_time - s.aimed_arrival_time END) AS d
        FROM stop_events s
        WHERE s.stop_point_ref = {stopRef}
        AND coalesce(s.actual_departure_time, s.actual_arrival_time) >= {since}
        )
        SELECT extract(hour FROM t AT TIME ZONE 'Europe/Oslo')::int AS hour,
        count(*)::int AS stops,
        round(percentile_cont(0.5) WITHIN GROUP (ORDER BY d)::numeric)::int AS median_s
        FROM delays
        GROUP BY 1
        ORDER BY 1
        """).ToListAsync(ct);
    }

    // Every stop (platform) that has data, for the stop search in the demo UI. A stop name can
    // differ between snapshots, so we take one per stop_point_ref.
    public async Task<IReadOnlyList<StopInfo>> GetStopsAsync(CancellationToken ct)
    {
        return await db.Database.SqlQuery<StopInfo>($"""
        SELECT stop_point_ref AS stop_ref, max(stop_point_name) AS name
        FROM stop_events
        WHERE stop_point_name IS NOT NULL
        GROUP BY stop_point_ref
        ORDER BY name, stop_ref
        """).ToListAsync(ct);
    }
}
