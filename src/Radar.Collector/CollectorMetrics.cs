using System.Diagnostics.Metrics;

namespace Radar.Collector;

public sealed class CollectorMetrics
{
    public const string MeterName = "Radar.Collector";

    private readonly Counter<long> _stopEventsSaved;
    private readonly Counter<long> _polls;
    private readonly Histogram<double> _pollDuration;

    public CollectorMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _stopEventsSaved = meter.CreateCounter<long>("radar.stop_events.saved", "{event}", "Stop events written to the database");
        _polls = meter.CreateCounter<long>("radar.polls", "{poll}", "Entur polls, by result");
        _pollDuration = meter.CreateHistogram<double>("radar.poll.duration", "s", "Time to fetch, parse and save one poll");
    }

    public void PollSucceeded(int savedRows, TimeSpan duration)
    {
        _stopEventsSaved.Add(savedRows);
        _polls.Add(1, new KeyValuePair<string, object?>("result", "success"));
        _pollDuration.Record(duration.TotalSeconds);
    }

    public void PollFailed() => _polls.Add(1, new KeyValuePair<string, object?>("result", "failure"));
}
