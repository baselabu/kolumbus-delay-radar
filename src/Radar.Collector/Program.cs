using Radar.Collector;
using Radar.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Radar.Collector.Entur;
using Radar.Collector.Storage;
using OpenTelemetry.Metrics;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<LineSyncService>();

builder.Services.AddDbContext<RadarDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Radar"))
    .UseSnakeCaseNamingConvention());

builder.Services.AddOptions<CollectorOptions>()
    .Bind(builder.Configuration.GetSection(CollectorOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart(); // Validate the options on startup, so we fail fast if something is misconfigured

var replayFolder = builder.Configuration[$"{CollectorOptions.Section}:ReplayFolder"];
if (string.IsNullOrEmpty(replayFolder))
{
    builder.Services.AddHttpClient<IEnturClient, HttpEnturClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<CollectorOptions>>().Value;
        client.DefaultRequestHeaders.Add("ET-Client-Name", options.ClientName);
    })
    .AddStandardResilienceHandler();
}
else
{
    builder.Services.AddSingleton<IEnturClient, FileEnturClient>();
}

builder.Services.AddHttpClient<IEnturLineClient, HttpEnturLineClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<CollectorOptions>>().Value;
    client.DefaultRequestHeaders.Add("ET-Client-Name", options.ClientName);
})
    .AddStandardResilienceHandler();

builder.Services.AddScoped<IStopEventWriter, PostgresStopEventWriter>();
builder.Services.AddScoped<ILineWriter, PostgresLineWriter>();
builder.Services.AddSingleton<SiriParser>();
builder.Services.AddSingleton<CollectorMetrics>();
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(CollectorMetrics.MeterName)  // our own counters
        .AddMeter("System.Net.Http")           // built in: duration and status of every call to Entur (429s!)
        .AddMeter("System.Runtime")            // built in: memory, GC
        .AddView("radar.poll.duration", new ExplicitBucketHistogramConfiguration
        {
            Boundaries = [0.5, 1, 2, 3, 5, 8, 13, 20, 30]   // polls take 2-6 s; the default buckets are made for milliseconds
        })
        .AddPrometheusHttpListener(o => o.ConfigureHttpListener = (_, listener) =>
        {

            listener.Prefixes.Clear();
            listener.Prefixes.Add("http://+:9464/metrics/");
        }));

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<RadarDbContext>();
    await dbContext.Database.MigrateAsync();
}
await host.RunAsync();
