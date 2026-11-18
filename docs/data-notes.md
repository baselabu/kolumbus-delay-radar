question 1

waht identify a Line: LineRef
what identify a Trip: DatedVehicleJourneyRef, with the uniqueness check as your proof (749 = 749)
what identify the Day: DataFrameRef

question 2:
RecordedCall vs EstimatedCall, and what Aimed / Expected / Actual mean.
stop allready passed, incomming stop, Aimed = the timetable (the plan), never changes. Expected = the prediction, changes every poll. Actual = what really happened, only on passed stops.

The unique key, and why trip + stop failed, with the 24-duplicates evidence.
its which day and which trip and which order ( position on the trip), it failed because a trip can visit the same stop twice and this would have a problem in the database.

The note about missing stop names.
some stops does not have names, so we should allow the names to be null in the data base.

“Delay is not stored; it’s calculated in SQL (actual − aimed), so the times stay the single source of truth.”

calculating delay:

            <RecordedCall>
              <StopPointRef>NSR:Quay:47794</StopPointRef>
              <Order>2</Order>
              <StopPointName>Oalsgata</StopPointName>
              <AimedArrivalTime>2026-10-16T16:36:00+02:00</AimedArrivalTime>
              <ActualArrivalTime>2026-10-16T16:37:40.293+02:00</ActualArrivalTime>
              <AimedDepartureTime>2026-10-16T16:36:00+02:00</AimedDepartureTime>
              <ActualDepartureTime>2026-10-16T16:37:40.293+02:00</ActualDepartureTime>
            </RecordedCall>

Delay = acutal arrival - aimed arrival = 100.293 seconds

the +02:00 time zone, "the clock is 2 hours a head of UTC", in the database we will store UTC times and only convert it back to Oslo time for rush hours or when someone asks for it.


## StopEvent design

| Field | XML element | C# type | Can be missing (?)? Why? |
|---|---|---|---|
| OperatingDate | DataFrameRef | DateOnly | no: every trip belongs to a day |
| JourneyRef | DatedVehicleJourneyRef | string | no: every trip has an ID |
| StopOrder | Order | int | no: every call has a position on the trip |
| LineRef | LineRef | string | no: every trip has a line (ID like "KOL:Line:8_1001", not a number) |
| StopRef | StopPointRef | string | no: every call has a stop (ID like "NSR:Quay:47794", not a number) |
| StopName | StopPointName | string? | yes: 333 stops have no name |
| AimedArrival | AimedArrivalTime | DateTimeOffset? | yes: first stop of a trip has no arrival |
| ExpectedArrival | ExpectedArrivalTime | DateTimeOffset? | yes: only on EstimatedCall (future stops) |
| ActualArrival | ActualArrivalTime | DateTimeOffset? | yes: only on RecordedCall (passed stops) |
| AimedDeparture | AimedDepartureTime | DateTimeOffset? | yes: last stop of a trip has no departure |
| ExpectedDeparture | ExpectedDepartureTime | DateTimeOffset? | yes: only on EstimatedCall (future stops) |
| ActualDeparture | ActualDepartureTime | DateTimeOffset? | yes: only on RecordedCall (passed stops) |
| Cancelled | Cancellation | bool | no: element missing means false |

## Test fixture trips

| Trip | Case | Recorded | Estimated | Special |
|---|---|---|---|---|
| _1043 | both | 37 | 5 | – |
| _1044 | only estimated | 0 | 12 | – |
| _1119 | only recorded | 30 | 0 | – |
| _2014 | no name | 2 | 3 | all 5 stops have no name |
| _2032 | cancelled | 43 | 2 | 1 stop cancelled |
| _2036 | same stop twice | 0 | 45 | Strand rådhus at order 30 and 44 |


# PostgreSQL

## Upsert rules (final)

| Column | Rule | SQL |
|---|---|---|
| expected_\*, actual_\* | newest wins, never NULL over a value | `COALESCE(EXCLUDED.x, stop_events.x)` |
| aimed_\* | first wins, fill in if missing | `COALESCE(stop_events.x, EXCLUDED.x)` |
| cancelled, line_ref, stop_point_ref | always overwrite | `EXCLUDED.x` |
| stop_point_name | newest wins, never NULL over a value | `COALESCE(EXCLUDED.x, stop_events.x)` |

# Performance

## Write performance: commit per row vs commit per poll

| | Before (1 commit per row) | After (1 commit per poll) |
|---|---|---|
| Full snapshot | 7,702 in 18,162 ms → 2.36 ms/event | 7,751 in 3,303 ms → 0.43 ms/event |
| Delta | 5,454 in 12,949 ms → 2.37 ms/event | 4,734 in 1,583 ms → 0.33 ms/event |

“one commit per row → one transaction per poll, this upgrade made it That’s about 5.5–7× faster

# Statistic problem in the database
Found a data artifact: line 6101, 13:35 trip: two stops stamped with the identical actual time 97 min late, rest of the trip without actual times, this is not a real delay.


## Query result: delay per stop on one trip

```
 stop_order |   stop_point_name    |   aimed_arrival_time   |      actual_arrival_time      |      delay
------------+----------------------+------------------------+-------------------------------+-----------------
          1 | Solbakk bussterminal |                        |                               |
          2 | Strandastøa          | 2026-10-24 13:35:00+00 | 2026-10-24 15:12:48.215704+00 | 01:37:48.215704
          3 | Giljebakken          | 2026-10-24 13:35:00+00 | 2026-10-24 15:12:48.215704+00 | 01:37:48.215704
          4 | Håbakk               | 2026-10-24 13:36:00+00 |                               |
          5 | Prestenesvegen       | 2026-10-24 13:36:00+00 |                               |
          6 | Prestamarka          | 2026-10-24 13:37:00+00 |                               |
          7 | Karlsakrossen        | 2026-10-24 13:38:00+00 |                               |
          8 | Bedehusvegen         | 2026-10-24 13:38:00+00 |                               |
          9 | Torgerkrossen vest   | 2026-10-24 13:39:00+00 |                               |
         10 | Tau bensinstasjon    | 2026-10-24 13:40:00+00 |                               |
         11 | Tau kai              | 2026-10-24 13:42:00+00 |                               |
(11 rows)
```


On-time rule: −60 s ≤ delay ≤ +180 s  (from 1 min early to 3 min late)
Reason: early is worse than late for passengers (you miss the bus); the 1-minute early
tolerance only covers rounding (aimed times are whole minutes).
Statistics: report median and p90, not average. Evidence: line 6101 avg 213 s vs
median 153 s, caused by one data artifact (97-minute "delay" that was a tracking glitch).


Clock change 2026-10-25 (CEST → CET): verified with real data.
- Grouped by UTC: no gaps/duplicates; offset to Oslo changes from +2 to +1 between
  00:00 and 02:00 UTC → AT TIME ZONE applies the correct offset per row.
- No trips were scheduled in the repeated hour (01:00–02:00 UTC = second 02:00 local).
- Storing UTC keeps every timestamp unambiguous; local times would make "02:30" ambiguous.


Night lines (N…) look extreme on the clock-change night 25 Oct; compare with a normal weekend night (1 Nov) before trusting them.


First rush-hour data (Mon 26 Oct, 07–09): express lines worst (X39: 3.1% on time, median 16 min). Line 6: 25% on time at rush vs 65% overall. Re-check after a full week

We measured, and decided not to add an index, here’s why, and here’s when we would.
Performance (EXPLAIN ANALYZE, ranking query, 2026-10-26, ~200k rows ≈ 3 days):
- Execution 563 ms: seq scan ~100 ms, sort ~170 ms (spilled to disk: 8.7 MB), median/p90 ~235 ms.
- The 7-day filter matches ~90% of rows → an index on the time column would not be used and
  would slow down every collector write. Decision: no index for now.
- Re-check when the table holds several weeks (filter < ~25% of rows): then add an expression
  index on coalesce(actual_departure_time, actual_arrival_time).
- The aggregation cost grows with the rows inside the window, not the table → output caching (Day 8).