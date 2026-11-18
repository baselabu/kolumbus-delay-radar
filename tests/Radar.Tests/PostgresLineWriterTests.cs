using Microsoft.EntityFrameworkCore;
using Radar.Collector.Storage;
using Radar.Data;

namespace Radar.Tests;

public class PostgresLineWriterTests : PostgresTestBase
{
    private static readonly Line Six = new("KOL:Line:TEST", "6", "Six", "bus");

    [Fact]
    public async Task Upserting_same_lines_twice_keeps_one_row()
    {
        var writer = new PostgresLineWriter(Db);
        await writer.UpsertLinesAsync([Six], CancellationToken.None);
        await writer.UpsertLinesAsync([Six], CancellationToken.None);

        Assert.Equal(1, await Db.Lines.CountAsync());
    }

    [Fact]
    public async Task Renamed_line_gets_new_public_code()
    {
        var writer = new PostgresLineWriter(Db);
        await writer.UpsertLinesAsync([Six], CancellationToken.None);
        var renamed = Six with { PublicCode = "6E" };
        await writer.UpsertLinesAsync([renamed], CancellationToken.None);

        var row = await Db.Lines.SingleAsync();
        Assert.Equal("6E", row.PublicCode);
    }

    [Fact]
    public async Task Missing_name_does_not_erase_known_name()
    {
        var writer = new PostgresLineWriter(Db);
        await writer.UpsertLinesAsync([Six], CancellationToken.None);
        var missingName = Six with { Name = null };
        await writer.UpsertLinesAsync([missingName], CancellationToken.None);

        var row = await Db.Lines.SingleAsync();
        Assert.Equal("Six", row.Name);
    }
}