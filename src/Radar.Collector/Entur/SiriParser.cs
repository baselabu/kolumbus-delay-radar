using System.Xml.Linq;
using Radar.Data;

namespace Radar.Collector.Entur;
public class SiriParser
{
    private static readonly XNamespace ns = "http://www.siri.org.uk/siri";

    public ParseResult ParseSiriXml(Stream xml)
    {
        // Load the XML document from the stream and make a list of StopEvent objects
        var doc = XDocument.Load(xml);
        var stopEvents =  new List<StopEvent>();
        var skippedTrips = 0;

            // Iterate through each EstimatedVehicleJourney element in the XML document
        foreach (var journey in doc.Descendants(ns + "EstimatedVehicleJourney"))
        {
            try
            {
                // which bus line is this journey for?
                var lineRef = Required(journey, "LineRef");

                // journey reference and the operating date for this journey
                var framed = journey.Element(ns + "FramedVehicleJourneyRef")!;
                var journeyRef = Required(framed, "DatedVehicleJourneyRef");
                var operatingDate = DateOnly.Parse(Required(framed, "DataFrameRef"));

                // Retrive the recorded (already happened) and estimated (will happen) calls for this journey
                var recordedCallElements = journey.Element(ns + "RecordedCalls")?.Elements(ns + "RecordedCall");
                var estimatedCallsElements = journey.Element(ns + "EstimatedCalls")?.Elements(ns + "EstimatedCall");

                // Combine the recorded and estimated calls into a single collection
                var callElements = (recordedCallElements ?? Enumerable.Empty<XElement>()).Concat(estimatedCallsElements ?? Enumerable.Empty<XElement>());

                // Convert each call element into a StopEvent object and add it to the list
                var tripEvents = new List<StopEvent>();
                foreach (var call in callElements)
                {
                    tripEvents.Add(ToStopEvent(call, journeyRef, operatingDate, lineRef));
                }
                stopEvents.AddRange(tripEvents);
            }
            catch (Exception)
            {
                // briken trip: skip it and continue with the next one
                skippedTrips++;
            }
        }
        return new ParseResult(stopEvents, skippedTrips);
    }

    private static StopEvent ToStopEvent(XElement call, string journeyRef, DateOnly operatingDate, string lineRef)
    {
        return new StopEvent(
            operatingDate,
            journeyRef,
            (int)call.Element(ns + "Order")!,
            lineRef,
            Required(call, "StopPointRef"),
            (string?)call.Element(ns + "StopPointName"),
            TimeToUtc(call, "AimedArrivalTime"),
            TimeToUtc(call, "ExpectedArrivalTime"),
            TimeToUtc(call, "ActualArrivalTime"),
            TimeToUtc(call, "AimedDepartureTime"),
            TimeToUtc(call, "ExpectedDepartureTime"),
            TimeToUtc(call, "ActualDepartureTime"),
            (bool?)call.Element(ns + "Cancellation") ?? false
        );
    }

    private static DateTimeOffset? TimeToUtc(XElement call, string elementName) =>
        ((DateTimeOffset?)call.Element(ns + elementName))?.ToUniversalTime();

    private static string Required(XElement parent, string name) =>
    (string?)parent.Element(ns + name)
    ?? throw new FormatException($"Missing <{name}>");

}
