#!/usr/bin/env bash
# Conformance: the correlation add-on. The caller's id is honoured, returned, and on the log lines of what it caused.
# Needs: PROJECT NAME LANG_SH WORK API_PORT EXAMPLE; with EXAMPLE = true a migrated database (after example-http.sh).
# Applies when: addons contains correlation.
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

PORT=$((API_PORT + 1))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done

SENT="$(python3 -I -c 'import uuid; print(uuid.uuid4())')"
RETURNED="$(curl -s -D - -o /dev/null -H "X-Correlation-Id: $SENT" "http://localhost:$PORT/health" | tr -d '\r' | awk -F': ' 'tolower($1) == "x-correlation-id" {print $2}')"
expect_equal "correlation: the id sent in the header comes back in the response" "$SENT" "$RETURNED"

MALFORMED="$(curl -s -D - -o /dev/null -w '%{http_code}' -H "X-Correlation-Id: not-a-guid" "http://localhost:$PORT/health" | tr -d '\r')"
expect_equal "correlation: a malformed header gets a new id, not an error" "200|True" "$(printf '%s' "$MALFORMED" | tail -c 3)|$(printf '%s' "$MALFORMED" | awk -F': ' 'tolower($1) == "x-correlation-id" {print $2}' | python3 -I -c 'import sys, uuid; v = sys.stdin.read().strip(); print(bool(v) and v != "not-a-guid" and bool(uuid.UUID(v)))')"

if [ "${EXAMPLE:-false}" = "true" ]; then
  REQUEST_ID="$(python3 -I -c 'import uuid; print(uuid.uuid4())')"
  curl -s -o /dev/null -X POST "http://localhost:$PORT/products" -H "Content-Type: application/json" -H "X-Correlation-Id: $REQUEST_ID" -d '{"sku":"COR-1","name":"Correlated","stock":5}'
  CUSTOMER="$(curl -s -X POST "http://localhost:$PORT/customers" -H "Content-Type: application/json" -H "X-Correlation-Id: $REQUEST_ID" -d '{"name":"Cor","email":"cor@example.com"}' | python3 -I -c 'import sys, json; print(json.load(sys.stdin)["id"])')"
  curl -s -o /dev/null -X POST "http://localhost:$PORT/orders" -H "Content-Type: application/json" -H "X-Correlation-Id: $REQUEST_ID" -d "{\"orderNumber\":\"COR-1\",\"customerId\":\"$CUSTOMER\",\"lines\":[{\"sku\":\"COR-1\",\"price\":1,\"currency\":\"EUR\",\"quantity\":2}]}"
  sleep 3
  LOG="$WORK/correlation.log"
  cat "$PROJECT/.mortis/api.log" > "$LOG"
  [ "${WORKER:-none}" = "separate" ] && [ -f "$PROJECT/.mortis/worker.log" ] && cat "$PROJECT/.mortis/worker.log" >> "$LOG"
  expect_equal "correlation: the command of the request logs the caller's id as its CorrelationId" "yes" "$([ "$(log_count "$LOG" "l.get('Message') == 'CreateOrderCommand started' and l.get('CorrelationId') == '$REQUEST_ID'")" -ge 1 ] && echo yes || echo no)"
  expect_equal "correlation: the event the request published carries it" "yes" "$([ "$(log_count "$LOG" "l.get('Event') == 'LineAdded' and l.get('CorrelationId') == '$REQUEST_ID'")" -ge 1 ] && echo yes || echo no)"
  expect_equal "correlation: the command an async handler sent carries it, caused by that event" "yes" "$([ "$(log_count "$LOG" "l.get('Message') == 'DecreaseProductStockCommand started' and l.get('CorrelationId') == '$REQUEST_ID' and l.get('CausationId') not in (None, '', 'None')")" -ge 1 ] && echo yes || echo no)"
fi

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
