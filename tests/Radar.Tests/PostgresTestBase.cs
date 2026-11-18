using Microsoft.EntityFrameworkCore;
using Radar.Data;
using Testcontainers.PostgreSql;

namespace Radar.Tests;

public abstract class PostgresTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:17").Build();
    protected RadarDbContext Db = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        var options = new DbContextOptionsBuilder<RadarDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        Db = new RadarDbContext(options);
        await Db.Database.MigrateAsync();
    }
    public async Task DisposeAsync()
    {
        await Db.DisposeAsync();
        await _pg.DisposeAsync();
    }
}

