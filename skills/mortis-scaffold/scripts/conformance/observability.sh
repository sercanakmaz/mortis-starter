#!/usr/bin/env bash
# Conformance: the observability add-on. Liveness and readiness, readiness on the database and the outbox lag, and one trace id
# from the request to the consumer, through the TraceId of the log lines (every transport, across the API and the Worker).
# NOT RUN YET (decision 48): written in phase 1 from observability.md, first run in phase 2.
# Needs: PROJECT NAME LANG_SH WORK DATABASE API_PORT OUTBOX WORKER, TOKEN with auth = jwt, a migrated PostgreSQL, psql, curl;
# the worker running when hosts.worker = separate.
# Applies when: addons contains observability, example = true, database = postgres; after example-http.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

S="$(date +%s%N)"
PORT=$((API_PORT + 11))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done
status() { curl -s -o "$WORK/observability.json" -w "%{http_code}" "$1"; }
check() { python3 -I -c "import json,sys; print(json.load(open(sys.argv[1]))['checks'].get(sys.argv[2], 'absent'))" "$WORK/observability.json" "$1" 2>/dev/null; }

expect_equal "observability: /health/live answers 200 without a token" "200" "$(status "http://localhost:$PORT/health/live")"
expect_equal "observability: /health/ready answers 200 with the database healthy" "200|Healthy" "$(status "http://localhost:$PORT/health/ready")|$(check database)"

if [ "${OUTBOX:-none}" != "none" ]; then
  CONSUMER_COLUMN="$(sql "select count(*) from information_schema.columns where table_name = 'outbox_messages' and column_name = 'consumer'")"
  LAG_ID="$(new_id)"
  sql "insert into outbox_messages (id, type, payload, occurred_at, attempts, next_attempt_at$([ "$CONSUMER_COLUMN" = 1 ] && echo ', consumer')) values ('$LAG_ID', 'lag-check', '{}', now() at time zone 'utc' - interval '1 hour', 0, now() at time zone 'utc' + interval '1 day'$([ "$CONSUMER_COLUMN" = 1 ] && echo ", 'lag-check'"))" >/dev/null
  expect_equal "observability: an outbox row older than the threshold makes /health/ready 503 on the outbox check" "503|Unhealthy" "$(status "http://localhost:$PORT/health/ready")|$(check outbox)"
  sql "delete from outbox_messages where id = '$LAG_ID'" >/dev/null
  expect_equal "observability: ... and ready again once it is gone" "200" "$(status "http://localhost:$PORT/health/ready")"
fi

api_curl -s -o /dev/null -H "Content-Type: application/json" -X POST -d "{\"sku\":\"OBS-$S\",\"name\":\"Traced\",\"stock\":5}" "http://localhost:$PORT/products"
CUSTOMER="$(api_curl -s -H "Content-Type: application/json" -X POST -d "{\"name\":\"Obs\",\"email\":\"obs-$S@example.com\"}" "http://localhost:$PORT/customers" | python3 -I -c 'import sys, json; print(json.load(sys.stdin)["id"])')"
api_curl -s -H "Content-Type: application/json" -X POST -d "{\"orderNumber\":\"OBS-$S\",\"customerId\":\"$CUSTOMER\",\"lines\":[{\"sku\":\"OBS-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":2}]}" -o /dev/null "http://localhost:$PORT/orders"
expect_equal "observability: the consumer decreased the stock (the trace below has something to link)" "3" "$(eventually "select stock from products where sku = 'OBS-$S'" "3" 20)"
LOG="$WORK/observability.log"
cat "$PROJECT/.mortis/api.log" > "$LOG"
[ "${WORKER:-none}" = "separate" ] && [ -f "$PROJECT/.mortis/worker.log" ] && cat "$PROJECT/.mortis/worker.log" >> "$LOG"
TRACE="$(log_field "$LOG" "l.get('Message') == 'CreateOrderCommand started' and l.get('TraceId') not in (None, '')" TraceId)"
expect_equal "observability: the order's command logs a trace id" "yes" "$([ -n "$TRACE" ] && echo yes || echo no)"
expect_equal "observability: the request log line of POST /orders carries the same trace id" "yes" "$([ "$(log_count "$LOG" "l.get('Method') == 'POST' and l.get('Route') == 'orders' and l.get('TraceId') == '$TRACE'")" -ge 1 ] && echo yes || echo no)"
expect_equal "observability: the consumer's command (in the API or the Worker) logs the same trace id" "yes" "$([ "$(log_count "$LOG" "l.get('Message') == 'DecreaseProductStockCommand started' and l.get('TraceId') == '$TRACE'")" -ge 1 ] && echo yes || echo no)"
expect_equal "observability: no request log line holds a body" "0" "$(log_count "$LOG" "l.get('Route') is not None and 'OBS-$S' in str(l)")"

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null

BAD_PORT=$((API_PORT + 12))
BAD_PID="$(ConnectionStrings__Default="Host=localhost;Port=1;Database=$DATABASE;Username=nobody;Password=none;Timeout=2" "$LANG_SH" api-start "$PROJECT" "$NAME" "$BAD_PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$BAD_PORT/health/live" >/dev/null 2>&1 && break; sleep 0.5; done
expect_equal "observability: with the database unreachable, live stays 200 and ready is 503 on the database check" "200|503|Unhealthy" "$(status "http://localhost:$BAD_PORT/health/live")|$(status "http://localhost:$BAD_PORT/health/ready")|$(check database)"
kill "$BAD_PID" 2>/dev/null
wait "$BAD_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
