using Radar.Collector.Storage;
using Radar.Collector.Entur;
using System.Diagnostics;

namespace Radar.Collector;

public class LineSyncService(IServiceScopeFactory scopeFactory, ILogger<LineSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            await SyncOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SyncOnceAsync(CancellationToken ct)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var scope = scopeFactory.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IEnturLineClient>();
            var writer = scope.ServiceProvider.GetRequiredService<ILineWriter>();

            var lines = await client.GetLinesAsync(ct);
            var count = await writer.UpsertLinesAsync(lines, ct);

            logger.LogInformation("Synced {LinesCount} lines in {ProcessingTime}ms", count, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            if (ct.IsCancellationRequested)
            {
                logger.LogInformation("Line sync is stopping due to cancellation request");
            }
            else
            {
                logger.LogWarning("Line sync was canceled unexpectedly");
            }

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Line sync failed");
        }
    }
}