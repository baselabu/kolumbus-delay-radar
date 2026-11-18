using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Radar.Collector.Entur;
using Radar.Data;

namespace Radar.Tests;
public class SiriParserTests
{
    private readonly List<StopEvent> _events;
    public SiriParserTests()
    {
        using var xml = File.OpenRead("fixtures/kol-small.xml");
        _events = new SiriParser().ParseSiriXml(xml).Events;
    }

    private List<StopEvent> Trip(string idEnd) =>
     _events.Where(e => e.JourneyRef.EndsWith(idEnd)).ToList();
    [Fact]
    public void Fixture_gives_179_events()
    {

        Assert.Equal(179, _events.Count);
    }

    [Fact]
    public void Trip_with_both_calls_gives_42_events()
    {
        var trip = Trip("_1043");
        Assert.Equal(42, trip.Count);
        Assert.Contains(trip, e => e.ActualDepartureTime != null);    // some stops passed
        Assert.Contains(trip, e => e.ActualDepartureTime == null);    // some still ahead
    }

    [Fact]
    public void Only_estimated_trip_has_no_actual_times()
    {
        var trip = Trip("_1044");
        Assert.Equal(12, trip.Count);
        Assert.All(trip, e => Assert.Null(e.ActualArrivalTime));
        Assert.All(trip, e => Assert.Null(e.ActualDepartureTime));
    }

    [Fact]
    public void Only_recorded_trip_has_actual_times()
    {
        var trip = Trip("_1119");
        var first = trip.Single(e => e.StopOrder == 1);
        var last = trip.MaxBy(e => e.StopOrder)!;
        Assert.All(trip, e => Assert.True(e.ActualArrivalTime != null || e.ActualDepartureTime != null));
        Assert.True(first.ActualArrivalTime == null && first.ActualDepartureTime != null);
        Assert.True(last.ActualArrivalTime != null && last.ActualDepartureTime == null);
    }

    [Fact]
    public void Stops_without_name_are_null()
    {
        var trip = Trip("_2014");
        Assert.Equal(5, trip.Count(e => e.StopPointName == null));
    }

    [Fact]
    public void Only_cancelled_stop_is_marked_cancelled()
    {
        var trip = Trip("_2032");
        Assert.Equal(1, trip.Count(e => e.Cancelled));
        Assert.Equal(45, trip.Count);
    }

    [Fact]
    public void Same_stop_twice_keeps_both_visits()
    {
        var trip = Trip("_2036");
        var orders = trip.Where(e => e.StopPointName == "Strand rådhus").Select(e => e.StopOrder);
        Assert.Equal(new[] { 30, 44 }, orders);
    }

    [Fact]
    public void All_times_are_utc()
    {
        var times = _events
            .SelectMany(e => new[] { e.AimedArrivalTime, e.ExpectedArrivalTime, e.ActualArrivalTime,
                                    e.AimedDepartureTime, e.ExpectedDepartureTime, e.ActualDepartureTime })
            .Where(t => t != null)
            .ToList();

        Assert.NotEmpty(times);
        Assert.All(times, t => Assert.Equal(TimeSpan.Zero, t!.Value.Offset));
    }

    [Fact]
    public void Broken_trip_is_skipped_and_counted()
    {
        var xml = """
            <Siri xmlns="http://www.siri.org.uk/siri"><ServiceDelivery><EstimatedTimetableDelivery><EstimatedJourneyVersionFrame>
                <EstimatedVehicleJourney>
                <FramedVehicleJourneyRef>
                    <DataFrameRef>2026-10-16</DataFrameRef>
                    <DatedVehicleJourneyRef>KOL:ServiceJourney:BROKEN</DatedVehicleJourneyRef>
                </FramedVehicleJourneyRef>
                <RecordedCalls>
                    <RecordedCall>
                    <StopPointRef>NSR:Quay:47794</StopPointRef>
                    <Order>2</Order>
                    <AimedDepartureTime>2026-10-16T16:36:00+02:00</AimedDepartureTime>
                    </RecordedCall>
                </RecordedCalls>
                </EstimatedVehicleJourney>
                <EstimatedVehicleJourney>
                <LineRef>KOL:Line:8_1001</LineRef>
                <FramedVehicleJourneyRef>
                    <DataFrameRef>2026-10-16</DataFrameRef>
                    <DatedVehicleJourneyRef>KOL:ServiceJourney:GOOD</DatedVehicleJourneyRef>
                </FramedVehicleJourneyRef>
                <RecordedCalls>
                    <RecordedCall>
                    <StopPointRef>NSR:Quay:47794</StopPointRef>
                    <Order>1</Order>
                    <AimedDepartureTime>2026-10-16T16:36:00+02:00</AimedDepartureTime>
                    </RecordedCall>
                </RecordedCalls>
                </EstimatedVehicleJourney>

            </EstimatedJourneyVersionFrame></EstimatedTimetableDelivery></ServiceDelivery></Siri>
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));

        var result = new SiriParser().ParseSiriXml(stream);
        Assert.Single(result.Events);
        Assert.Equal(1, result.SkippedTrips);
    }
}