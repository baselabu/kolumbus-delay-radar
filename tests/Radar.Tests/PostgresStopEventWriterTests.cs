using Microsoft.EntityFrameworkCore;
using Radar.Collector.Storage;
using Radar.Collector.Entur;
using Radar.Data;

namespace Radar.Tests;

public class PostgresStopEventWriterTests : PostgresTestBase
{

    [Fact]
    public async Task Upserting_same_events_twice_keeps_179_rows()
    {
        using var xml = File.OpenRead("fixtures/kol-small.xml");
        var events = new SiriParser().ParseSiriXml(xml).Events;
        var writer = new PostgresStopEventWriter(Db);
        // calling upsert twice should insert the first time, update the second time
        await writer.UpsertAsync(events, CancellationToken.None);
        await writer.UpsertAsync(events, CancellationToken.None);

        Assert.Equal(179, await Db.StopEvents.CountAsync());
    }

    [Fact]
    public async Task Upserting_actual_time_is_not_overwritten_by_null()
    {
        var original = new StopEvent
        (
            new DateOnly(2024, 6, 1),
    "KOL:ServiceJourney:TEST",
        1,
        "KOL:Line:8_1001",
    "NSR:Quay:1",
    "Test Stop",
    new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero),
    new DateTimeOffset(2024, 6, 1, 12, 5, 0, TimeSpan.Zero),
    new DateTimeOffset(2024, 6, 1, 12, 10, 0, TimeSpan.Zero),
    new DateTimeOffset(2024, 6, 1, 12, 15, 0, TimeSpan.Zero),
    new DateTimeOffset(2024, 6, 1, 12, 20, 0, TimeSpan.Zero),
    new DateTimeOffset(2024, 6, 1, 12, 25, 0, TimeSpan.Zero),
    false
        );

        var withoutActual = original with
        {
            ActualArrivalTime = null,
            ActualDepartureTime = null
        };

        var writer = new PostgresStopEventWriter(Db);
        await writer.UpsertAsync(new[] { original }, CancellationToken.None);
        await writer.UpsertAsync(new[] { withoutActual }, CancellationToken.None);

        var row = await Db.StopEvents.SingleAsync();
        Assert.Equal(original.ActualArrivalTime, row.ActualArrivalTime);
        Assert.Equal(original.ActualDepartureTime, row.ActualDepartureTime);
    }
}