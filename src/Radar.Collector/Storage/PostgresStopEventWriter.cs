using Microsoft.EntityFrameworkCore;
using Radar.Data;

namespace Radar.Collector.Storage;

public class PostgresStopEventWriter(RadarDbContext db) : IStopEventWriter
{
    public async Task<int> UpsertAsync(IReadOnlyList<StopEvent> events, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        foreach (var e in events)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO stop_events

                (operating_date, journey_ref, stop_order, line_ref, stop_point_ref, stop_point_name,
                aimed_arrival_time, expected_arrival_time, actual_arrival_time,
                aimed_departure_time, expected_departure_time, actual_departure_time,
                cancelled)

                VALUES
                ({e.OperatingDate}, {e.JourneyRef}, {e.StopOrder}, {e.LineRef}, {e.StopPointRef}, {e.StopPointName},
                {e.AimedArrivalTime}, {e.ExpectedArrivalTime}, {e.ActualArrivalTime},
                {e.AimedDepartureTime}, {e.ExpectedDepartureTime}, {e.ActualDepartureTime},
                {e.Cancelled})

                ON CONFLICT (operating_date, journey_ref, stop_order) DO UPDATE SET
                -- always overwrites these values
                line_ref = EXCLUDED.line_ref,
                stop_point_ref = EXCLUDED.stop_point_ref,
                cancelled = EXCLUDED.cancelled,

                -- newest wins, never NULL over a value
                stop_point_name = COALESCE(EXCLUDED.stop_point_name, stop_events.stop_point_name),
                expected_arrival_time   = COALESCE(EXCLUDED.expected_arrival_time, stop_events.expected_arrival_time),
                actual_arrival_time     = COALESCE(EXCLUDED.actual_arrival_time, stop_events.actual_arrival_time),
                expected_departure_time = COALESCE(EXCLUDED.expected_departure_time, stop_events.expected_departure_time),
                actual_departure_time   = COALESCE(EXCLUDED.actual_departure_time, stop_events.actual_departure_time),

                -- first wins, fill in if missing (note: old value FIRST)
                aimed_arrival_time      = COALESCE(stop_events.aimed_arrival_time, EXCLUDED.aimed_arrival_time),
                aimed_departure_time    = COALESCE(stop_events.aimed_departure_time, EXCLUDED.aimed_departure_time)
                """, ct);
        }
        await transaction.CommitAsync(ct);
        return events.Count;
    }
}
