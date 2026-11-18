using Radar.Data;
using Microsoft.EntityFrameworkCore;

namespace Radar.Collector.Storage;
public class PostgresLineWriter(RadarDbContext db) : ILineWriter
{
    public async Task<int> UpsertLinesAsync(IReadOnlyList<Line> lines, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        foreach (var line in lines)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO lines (line_ref, public_code, name, transport_mode)
                VALUES ({line.LineRef}, {line.PublicCode}, {line.Name}, {line.TransportMode})
                ON CONFLICT (line_ref) DO UPDATE SET
                public_code = COALESCE(EXCLUDED.public_code, lines.public_code),
                name = COALESCE(EXCLUDED.name, lines.name),
                transport_mode = COALESCE(EXCLUDED.transport_mode, lines.transport_mode)
                """, ct);
        }
        await transaction.CommitAsync(ct);
        return lines.Count;
    }
}