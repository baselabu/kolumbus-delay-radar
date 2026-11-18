using Radar.Collector.Storage;
using Radar.Collector.Entur;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Radar.Collector;

public class Worker(
    IServiceScopeFactory scopeFactory,
    SiriParser parser,
    IOptions<CollectorOptions> options,
    ILogger<Worker> logger,
    CollectorMetrics metrics) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds));
        do
        {
            await PollOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var scope = scopeFactory.CreateScope();
            var entur = scope.ServiceProvider.GetRequiredService<IEnturClient>();
            var writer = scope.ServiceProvider.GetRequiredService<IStopEventWriter>();

            await using var stream = await entur.GetEstimatedTimetableAsync(ct);
            var (events, skippedTrips) = parser.ParseSiriXml(stream);

            var rowCount = await writer.UpsertAsync(events, ct);

            logger.LogInformation("Saved {RowCount} stop events to the database, skipped {SkippedTripsCount} trips in {ProcessingTime}ms", rowCount, skippedTrips, sw.ElapsedMilliseconds);

            metrics.PollSucceeded(rowCount, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
            {
                logger.LogInformation("Worker is stopping due to cancellation request");
            }
            else
            {
                logger.LogWarning("Worker polling was canceled unexpectedly");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Poll failed");
            metrics.PollFailed();
        }
    }
}
