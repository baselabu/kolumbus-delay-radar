using Microsoft.Extensions.Time.Testing;
using Radar.Api.Repositories;
using Radar.Api.Services;

namespace Radar.Tests;

public class LiveDelayServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);

    private static RecentStop Stop(string trip, DateTimeOffset aimed, int delaySeconds) =>
        new(trip, "6", "Test stop", aimed, aimed.AddSeconds(delaySeconds));

    [Fact]
    public async Task Trip_is_reported_once_with_its_latest_stop()
    {
        var repo = new FakeRepository(
            Stop("trip1", Now.AddMinutes(-15), 120),
            Stop("trip1", Now.AddMinutes(-10), 480)
            );
        var service = new LiveDelayService(repo, new FakeTimeProvider(Now));

        var result = await service.GetLateBusesAsync(minDelayMinutes: 3, CancellationToken.None);

        var bus = Assert.Single(result);
        Assert.Equal(480, bus.DelaySeconds);
    }

    [Fact]
    public async Task Asks_repository_for_the_last_15_minutes()
    {
        var repo = new FakeRepository(Stop("trip1", Now.AddMinutes(-10), 300));
        var service = new LiveDelayService(repo, new FakeTimeProvider(Now));

        await service.GetLateBusesAsync(minDelayMinutes: 3, CancellationToken.None);

        Assert.Equal(Now - DelayRules.LiveWindow, repo.AskedSince);
    }
    [Fact]
    public async Task Exactly_on_the_limit_is_not_late_one_second_more_is()
    {
        var repo = new FakeRepository(
            Stop("on-limit", Now.AddMinutes(-10), 180),     // exactly 3 min
            Stop("over", Now.AddMinutes(-10), 181));    // 3 min + 1 s
        var service = new LiveDelayService(repo, new FakeTimeProvider(Now));

        var result = await service.GetLateBusesAsync(minDelayMinutes: 3, CancellationToken.None);

        var bus = Assert.Single(result);
        Assert.Equal("over", bus.JourneyRef);
    }

    [Fact]
    public async Task Most_late_bus_comes_first()
    {
        var repo = new FakeRepository(
            Stop("a", Now.AddMinutes(-12), 200),
            Stop("b", Now.AddMinutes(-12), 600),
            Stop("c", Now.AddMinutes(-12), 400));
        var service = new LiveDelayService(repo, new FakeTimeProvider(Now));

        var result = await service.GetLateBusesAsync(minDelayMinutes: 3, CancellationToken.None);

        Assert.Equal(new[] { 600, 400, 200 }, result.Select(b => b.DelaySeconds));
    }

    [Fact]
    public async Task Early_bus_is_not_reported()
    {
        var repo = new FakeRepository(Stop("early", Now.AddMinutes(-10), -120));   // 2 min early
        var service = new LiveDelayService(repo, new FakeTimeProvider(Now));

        var result = await service.GetLateBusesAsync(minDelayMinutes: 3, CancellationToken.None);

        Assert.Empty(result);
    }

    private class FakeRepository(params RecentStop[] stops) : IStopEventReadRepository
    {
        public DateTimeOffset? AskedSince { get; private set; }

        public Task<IReadOnlyList<RecentStop>> GetStopsPassedSinceAsync(DateTimeOffset since, CancellationToken ct)
        {
            AskedSince = since;
            return Task.FromResult<IReadOnlyList<RecentStop>>(stops);
        }
    }
        [Theory]
    [InlineData(1, 1)]    // January: winter time, UTC+1
    [InlineData(7, 2)]    // July: summer time, UTC+2
    public async Task Times_are_returned_in_Oslo_time(int month, int expectedOffsetHours)
    {
        var now = new DateTimeOffset(2026, month, 15, 12, 0, 0, TimeSpan.Zero);
        var repo = new FakeRepository(Stop("trip1", now.AddMinutes(-10), 300));
        var service = new LiveDelayService(repo, new FakeTimeProvider(now));

        var bus = Assert.Single(await service.GetLateBusesAsync(minDelayMinutes: 3, CancellationToken.None));

        Assert.Equal(TimeSpan.FromHours(expectedOffsetHours), bus.Aimed.Offset);
        Assert.Equal(TimeSpan.FromHours(expectedOffsetHours), bus.Actual.Offset);
        Assert.Equal(now.AddMinutes(-10), bus.Aimed);   // still the same moment, only written in Oslo time
    }
}
