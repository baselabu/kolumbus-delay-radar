using Microsoft.EntityFrameworkCore;
using Radar.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Radar.Api.Repositories;
using Radar.Api.Services;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddDbContext<RadarDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Radar"))
           .UseSnakeCaseNamingConvention()
           .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<RadarDbContext>(
        name: "database",
        tags: ["ready"],
        customTestQuery: async (db, ct) => { await db.StopEvents.AnyAsync(ct); return true; });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("per-client", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1)
            }));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();   // trust the proxy (the Ingress) inside the cluster
    options.KnownProxies.Clear();
});

builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("live", p => p.Expire(TimeSpan.FromSeconds(30)));
    options.AddPolicy("stats", p => p.Expire(TimeSpan.FromMinutes(10)).SetVaryByQuery("days", "period"));
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IStopEventReadRepository, StopEventReadRepository>();
builder.Services.AddScoped<ILiveDelayService, LiveDelayService>();
builder.Services.AddScoped<IStatisticsRepository, StatisticsRepository>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();

builder.Services.AddProblemDetails();

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()   // requests, response times, status codes, rate-limit rejections
        .AddMeter("System.Runtime")       // .NET's built-in metrics: memory, GC, threads
        .AddPrometheusExporter());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseDefaultFiles();   // "/" serves wwwroot/index.html, the demo UI
app.UseStaticFiles();
app.UseRateLimiter();
app.UseOutputCache();

app.MapControllers().RequireRateLimiting("per-client");
app.MapGet("/api", () => Results.Redirect("/"));   // "/api" and "/api/" lead to the demo UI
app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/readyz", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapPrometheusScrapingEndpoint();

app.Run();
public partial class Program{}

