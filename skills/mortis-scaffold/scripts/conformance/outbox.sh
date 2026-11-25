#!/usr/bin/env bash
# Conformance: the publishing model of events.outbox. Rows in the command's transaction, deleted once published, retried, latency.
# Needs: PROJECT NAME LANG_SH WORK DATABASE API_PORT OUTBOX WORKER, a migrated database (after example-console.sh), psql, curl.
# Applies when: example = true, database = postgres. With events.outbox = none it only proves that no outbox exists.
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

TABLE="$(sql "select count(*) from information_schema.tables where table_name = 'outbox_messages'")"

if [ "$OUTBOX" = "none" ]; then
  expect_equal "outbox none: the database has no outbox table" "0" "$TABLE"
  expect_equal "outbox none: no outbox type or setting in the project" "0" "$(grep -rlE 'OutboxMessage|"Outbox"' "$PROJECT/src" 2>/dev/null | wc -l | tr -d ' ')"
  report
  [ "$FAILED" -eq 0 ]
  return 0 2>/dev/null || exit 0
fi

expect_equal "outbox: the outbox table exists" "1" "$TABLE"
expect_equal "outbox: the project has no publisher for the in-process model" "0" "$(grep -rl 'class EventPublisher' "$PROJECT/src" 2>/dev/null | wc -l | tr -d ' ')"
if [ "$OUTBOX" = "background" ]; then
  expect_equal "outbox background: no after-command code is generated" "0" "$(grep -rlE 'ClaimAsync\(Guid|outboxWorker\.PublishAsync' "$PROJECT/src" 2>/dev/null | wc -l | tr -d ' ')"
fi

S="$(python3 -I -c 'import uuid; print(uuid.uuid4().hex[:8])')"
PORT=$((API_PORT + 2))
RELAY_PID=""
start_relay() {
  if [ "$WORKER" = "separate" ]; then
    RELAY_PID="$("$LANG_SH" worker-start "$PROJECT" "$NAME")"
    RELAY_LOG="$PROJECT/.mortis/worker.log"
    sleep 4
  else
    RELAY_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
    RELAY_LOG="$PROJECT/.mortis/api.log"
    for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done
  fi
}
stop_relay() { [ -n "$RELAY_PID" ] && { kill "$RELAY_PID" 2>/dev/null; wait "$RELAY_PID" 2>/dev/null; RELAY_PID=""; }; }
run() { console_run "$@" 2>>"$WORK/outbox.log"; }
id_of() { json_field "$1" "['Id']"; }
: > "$WORK/outbox.log"

COMPLETED="$(wire_name Orders Completed | tr ':' '.')"
CUSTOMER="$(id_of "$(run CreateCustomer "{\"name\":\"Outbox\",\"email\":\"outbox-$S@example.com\"}")")"
run CreateProduct "{\"sku\":\"OBX-$S\",\"name\":\"Outbox product\",\"stock\":10}" >/dev/null
ORDER="$(id_of "$(run CreateOrder "{\"orderNumber\":\"OBX-$S\",\"customerId\":$(jid "$CUSTOMER"),\"lines\":[{\"sku\":\"OBX-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":3}]}")")"

if [ "$OUTBOX" = "after-command" ]; then
  expect_equal "latency after-command: the event consumer had run when the command returned" "7" "$(sql "select stock from products where sku = 'OBX-$S'")"
  expect_equal "after-command: the published row is deleted" "0" "$(sql "select count(*) from outbox_messages where payload::text like '%OBX-$S%'")"
else
  expect_equal "background: the command only wrote the row; nothing ran yet" "10|1" "$(sql "select (select stock from products where sku = 'OBX-$S') || '|' || count(*) from outbox_messages where payload::text like '%OBX-$S%'")"
  start_relay
  START="$(date +%s%N)"
  STOCK="$(eventually "select stock from products where sku = 'OBX-$S'" "7")"
  ELAPSED=$(( ($(date +%s%N) - START) / 1000000 ))
  expect_equal "latency background: the worker delivered within three polling intervals (one to claim, the broker and the consumer, one of slack under load)" "7|yes" "$STOCK|$([ "$ELAPSED" -le 3000 ] && echo yes || echo no)"
  expect_equal "background: the published row is deleted" "0" "$(eventually "select count(*) from outbox_messages where payload::text like '%OBX-$S%'" "0")"
  sleep 0.5
  ROW_LOG="$RELAY_LOG"
fi

ROW_LOG="${ROW_LOG:-$WORK/outbox.log}"
LINE_ADDED="$(log_field "$ROW_LOG" "l.get('Message', '').startswith('Handled') and l.get('Event') == '$(event_name Orders LineAdded)' and l.get('CorrelationId') == '$(log_field "$WORK/outbox.log" "l.get('Message') == 'CreateOrderCommand started' and True" CorrelationId)'" EventId)"
expect_equal "context: the command the event consumer sent carries the row's correlation and is caused by the row" "1" "$(log_count "$ROW_LOG" "l.get('Message') == 'DecreaseProductStockCommand started' and l.get('CausationId') == '$LINE_ADDED'")"


stop_relay
run BlockCustomer "{\"id\":$(jid "$CUSTOMER")}" >/dev/null
console_run CompleteOrder "{\"id\":$(jid "$ORDER")}" >/dev/null 2>>"$WORK/outbox.log"
expect_equal "atomicity: a command that rolled back left no outbox row (no worker running)" "0|0" "$(sql "select count(*) from outbox_messages where type = '$COMPLETED' and payload->>'Id' = '$ORDER'")|$(log_count "$WORK/outbox.log" "l.get('Event') == '$(event_name Orders Completed)'")"
if [ "${TRANSPORT:-database}" != "rabbitmq" ]; then
  run CreateOrder "{\"orderNumber\":\"OBF-$S\",\"customerId\":\"$(id_of "$(run CreateCustomer "{\"name\":\"Over\",\"email\":\"over-$S@example.com\"}")")\",\"lines\":[{\"sku\":\"OBX-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":50}]}" >/dev/null
  start_relay
  RETRIES="$(grep -rhoE 'DecreaseStockWhenOrderLineAddedEventConsumer>\(endpoint => endpoint\.Retry\([0-9]+\)' "$PROJECT/src" | grep -oE '[0-9]+\)$' | tr -d ')' | head -1)"
  ATTEMPTS=$(( ${RETRIES:-3} + 1 ))
  OVER="payload->>'Sku' = 'OBX-$S' and (payload->>'Quantity')::int = 50"
  for _ in $(seq 1 600); do
    [ "$(sql "select attempts from outbox_messages where $OVER")" = "$ATTEMPTS" ] && break
    sql "update outbox_messages set next_attempt_at = now() - interval '1 second' where $OVER and attempts < max_attempts" >/dev/null
    sleep 0.1
  done
  expect_equal "failure: a consumer that throws leaves its row with the error, and the row stays at the consumer's Retry(n) + 1 attempts" "$ATTEMPTS|$ATTEMPTS|yes" "$(eventually "select attempts || '|' || max_attempts || '|' || case when last_error is not null then 'yes' else 'no' end from outbox_messages where payload->>'Sku' = 'OBX-$S' and (payload->>'Quantity')::int = 50" "$ATTEMPTS|$ATTEMPTS|yes" 60)"
fi
stop_relay

report
[ "$FAILED" -eq 0 ]
