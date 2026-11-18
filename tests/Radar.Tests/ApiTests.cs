using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Radar.Api.Repositories;
using Radar.Collector.Storage;
using Radar.Data;
using Testcontainers.PostgreSql;

namespace Radar.Tests;

public class ApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:17").Build();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private RadarDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();

        var options = new DbContextOptionsBuilder<RadarDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        _db = new RadarDbContext(options);
        await _db.Database.MigrateAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Radar", _pg.GetConnectionString()));
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
        await _pg.DisposeAsync();
    }

    private async Task SeedLineAsync(string lineRef, string publicCode, int stops, int delaySeconds)
    {
        var aimed = DateTimeOffset.UtcNow.AddHours(-1);
        var events = Enumerable.Range(1, stops).Select(order => new StopEvent(
            DateOnly.FromDateTime(aimed.UtcDateTime), $"{lineRef}:trip", order, lineRef,
            $"NSR:Quay:{order}", $"Stop {order}",
            null, null, null,                                        // no arrival times
            aimed, null, aimed.AddSeconds(delaySeconds),             // aimed + actual departure
            false)).ToList();

        await new PostgresStopEventWriter(_db).UpsertAsync(events, CancellationToken.None);
        await new PostgresLineWriter(_db).UpsertLinesAsync([new Line(lineRef, publicCode, publicCode, "bus")], CancellationToken.None);
    }

    [Fact]
    public async Task Stops_lists_every_stop_with_its_name()
    {
        await SeedLineAsync("KOL:Line:TEST_S", "S", stops: 3, delaySeconds: 0);

        var stops = await _client.GetFromJsonAsync<List<StopInfo>>("/api/stops");

        Assert.Equal(3, stops!.Count);
        Assert.Contains(new StopInfo("NSR:Quay:1", "Stop 1"), stops);
    }

    [Fact]
    public async Task Front_page_serves_the_demo_ui()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Kolumbus Delay Radar", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/")]
    public async Task Api_root_redirects_to_the_demo_ui(string path)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Ranking_includes_line_with_50_stops_but_not_49()
    {
        await SeedLineAsync("KOL:Line:TEST_A", "A", stops: 50, delaySeconds: 60);
        await SeedLineAsync("KOL:Line:TEST_B", "B", stops: 49, delaySeconds: 60);

        var ranking = await _client.GetFromJsonAsync<List<LinePunctuality>>("/api/lines/ranking");

        var line = Assert.Single(ranking!);
        Assert.Equal("A", line.Line);
    }

    [Fact]
    public async Task Punctuality_returns_correct_statistics()
    {
        await SeedLineAsync("KOL:Line:TEST_C", "C", stops: 7, delaySeconds: 60);
        await SeedLineAsync("KOL:Line:TEST_C2", "C", stops: 3, delaySeconds: 600);

        var result = await _client.GetFromJsonAsync<LinePunctuality>("/api/lines/C/punctuality");

        Assert.Equal(10, result!.Stops);
        Assert.Equal(70.0m, result.OnTimePct);
        Assert.Equal(60, result.MedianS);
        Assert.Equal(600, result.P90S);
    }

    [Fact]
    public async Task Unknown_line_returns_404_problem_details()
    {
        var response = await _client.GetAsync("/api/lines/does-not-exist/punctuality");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Too_many_days_returns_400()
    {
        var response = await _client.GetAsync("/api/lines/ranking?days=40");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}