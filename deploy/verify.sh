#!/usr/bin/env bash
# Checks that the deployed system works end to end. Exit code 0 = all checks passed.
# Read-only, except for one throwaway pod used to test the firewall.
set -uo pipefail                  # no -e: run every check and report all failures
cd "$(dirname "$0")/.."

NS=delay-radar
LINE=6                            # a busy Kolumbus line
STOP=NSR:Quay:46957               # Vaktapoteket, Stavanger centre
HOST=$(kubectl -n "$NS" get gateway radar-gateway -o jsonpath='{.spec.listeners[0].hostname}')
failed=0

check() {                         # check "description" command...
  local name=$1; shift
  if "$@" >/dev/null 2>&1; then echo "PASS  $name"; else echo "FAIL  $name"; failed=$((failed + 1)); fi
}
equals() { [ "$1" = "$2" ]; }
http_code() { curl -s -o /dev/null -w '%{http_code}' --max-time 15 "$1"; }
prom_query() {                    # instant PromQL query through the Kubernetes API server proxy
  kubectl get --raw "/api/v1/namespaces/monitoring/services/kps-kube-prometheus-stack-prometheus:9090/proxy/api/v1/query?query=$1" \
    | python3 -c 'import json,sys; r=json.load(sys.stdin)["data"]["result"]; print(r[0]["value"][1] if r else 0)'
}
newest_row_age_min() {            # minutes since the newest saved stop event
  kubectl -n "$NS" exec postgres-0 -- psql -U radar -d radar -tAc \
    "select extract(epoch from now() - max(coalesce(actual_departure_time, actual_arrival_time)))::int / 60 from stop_events"
}
last_backup_age_h() {             # hours since the backup CronJob last succeeded
  local t; t=$(kubectl -n "$NS" get cronjob backup -o jsonpath='{.status.lastSuccessfulTime}')
  [ -n "$t" ] && echo $(( ($(date +%s) - $(date -d "$t" +%s)) / 3600 ))
}
below() { [ -n "$1" ] && [ "$1" -lt "$2" ]; }
at_least() { [ -n "$1" ] && [ "${1%.*}" -ge "$2" ]; }

echo "Verifying https://$HOST"
echo "--- Cluster"
check "all nodes Ready"                 bash -c '! kubectl get nodes --no-headers | grep -v " Ready "'
check "postgres ready"                  kubectl -n "$NS" wait --for=condition=Ready pod/postgres-0 --timeout=60s
check "collector ready"                 kubectl -n "$NS" wait --for=condition=Ready pod -l app.kubernetes.io/name=collector --timeout=60s
check "api ready (all replicas)"        kubectl -n "$NS" wait --for=condition=Ready pod -l app.kubernetes.io/name=api --timeout=60s
check "TLS certificate ready"           equals "$(kubectl -n "$NS" get certificate radar-tls -o jsonpath='{.status.conditions[?(@.type=="Ready")].status}')" True

echo "--- Public endpoint"
check "GET / = demo UI"                     bash -c "curl -sf --max-time 15 https://$HOST/ | grep -q 'Kolumbus Delay Radar'"
check "GET /api/live/late = 200 (HTTPS, trusted certificate)"  equals "$(http_code "https://$HOST/api/live/late")" 200
check "GET /api/lines/$LINE/punctuality = 200"   equals "$(http_code "https://$HOST/api/lines/$LINE/punctuality?days=1")" 200
check "GET /api/lines/ranking = 200"             equals "$(http_code "https://$HOST/api/lines/ranking?days=1")" 200
check "GET /api/stops = 200 (stop search)"      equals "$(http_code "https://$HOST/api/stops")" 200
check "GET /api/stops/$STOP/delays = 200"        equals "$(http_code "https://$HOST/api/stops/$STOP/delays?days=1")" 200
check "HTTP redirects to HTTPS (301)"   equals "$(http_code "http://$HOST/api/live/late")" 301
check "/readyz is not public (404)"     equals "$(http_code "https://$HOST/readyz")" 404

echo "--- Data and operations"
# Success criterion from design.md: new stop events are ingested within 60 seconds
# increase() is reset-safe: right after a deployment the new collector's counter starts at 0
echo "      (waiting 65 s for new stop events...)"
sleep 65
saved=$(prom_query 'sum(increase(radar_stop_events_saved_total%5B65s%5D))')   # [65s], URL-encoded
check "new stop events ingested within 60 s"      python3 -c "import sys; sys.exit(not float('$saved') > 0)"
check "newest row in the database < 10 min old"   below "$(newest_row_age_min)" 10
check "backup succeeded in the last 26 h"         below "$(last_backup_age_h)" 26
check "Prometheus scrapes api + collector (3 up)" at_least "$(prom_query 'count(up%7Bnamespace%3D%22delay-radar%22%7D%3D%3D1)')" 3

echo "--- Security"
# shellcheck source=../scripts/nettest.sh
source scripts/nettest.sh
check "unknown pod cannot reach postgres"  bash -c "$(declare -f nettest); nettest postgres 5432 | grep -q BLOCKED"

echo
if [ "$failed" -eq 0 ]; then echo "All checks passed."; else echo "$failed check(s) failed."; fi
exit "$failed"
