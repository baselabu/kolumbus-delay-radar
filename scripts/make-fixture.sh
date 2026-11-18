#!/usr/bin/env bash
# Builds a small SIRI-ET fixture with one trip per tricky case.
# Usage: make-fixture.sh <full-snapshot.xml> <output.xml>
set -euo pipefail
src=$1; out=$2
J="//*[local-name()='EstimatedVehicleJourney']"
REF="*[local-name()='FramedVehicleJourneyRef']/*[local-name()='DatedVehicleJourneyRef']"
CALL="*[local-name()='EstimatedCall' or local-name()='RecordedCall']"

first_id() {  # first trip ID matching an XPath filter
  xmllint --xpath "string(($J[$1])[1]/$REF)" "$src"
}

declare -A cases=(
  [both]="*[local-name()='RecordedCalls'] and *[local-name()='EstimatedCalls']"
  [only-estimated]="not(*[local-name()='RecordedCalls'])"
  [only-recorded]="not(*[local-name()='EstimatedCalls'])"
  [cancelled]=".//*[local-name()='Cancellation']='true'"
  [no-name]=".//$CALL[not(*[local-name()='StopPointName'])]"
)
ids=()
for c in both only-estimated only-recorded cancelled no-name; do
  id=$(first_id "${cases[$c]}"); echo "$c: ${id:-NOT FOUND}"; [ -n "$id" ] && ids+=("$id")
done
twice="KOL:ServiceJourney:6100_250903083239535_2036"   # visits Strand rådhus twice (found 2026-10-20)
echo "same-stop-twice: $twice"; ids+=("$twice")

{
  echo '<?xml version="1.0" encoding="UTF-8"?>'
  echo '<Siri xmlns="http://www.siri.org.uk/siri" version="2.0"><ServiceDelivery>'
  echo '<EstimatedTimetableDelivery version="2.0"><EstimatedJourneyVersionFrame>'
  for id in $(printf '%s\n' "${ids[@]}" | sort -u); do
    xmllint --xpath "$J[$REF='$id']" "$src"; echo
  done
  echo '</EstimatedJourneyVersionFrame></EstimatedTimetableDelivery></ServiceDelivery></Siri>'
} | xmllint --format - > "$out"
echo "wrote $out ($(xmllint --xpath "count($J)" "$out") trips, $(wc -c < "$out") bytes)"
