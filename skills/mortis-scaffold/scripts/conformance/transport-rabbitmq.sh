#!/usr/bin/env bash
# Conformance: events.transport = rabbitmq. Rows published to the broker, consumed by each consumer's queue, failures in <queue>_error with the
# MassTransit fault headers, and a broker that is down only delays delivery.
# Needs: PROJECT NAME LANG_SH WORK DATABASE, a migrated database, a RabbitMQ with the management plugin (RABBITMQ_API,
# default http://guest:guest@localhost:15672), psql, curl.
# Applies when: events.transport = rabbitmq, example = true.
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

API="${RABBITMQ_API:-http://guest:guest@localhost:15672}/api"
QUEUE="decrease-stock-when-order-line-added"
S="$(python3 -I -c 'import uuid; print(uuid.uuid4().hex[:8])')"
run() { console_run "$@" 2>>"$WORK/rabbitmq.log"; }
id_of() { json_field "$1" "['Id']"; }
broker() { curl -s "$API/$1"; }
start_worker() { WORKER_PID="$("$LANG_SH" worker-start "$PROJECT" "$NAME")"; sleep 4; }
stop_worker() { kill "$WORKER_PID" 2>/dev/null; wait "$WORKER_PID" 2>/dev/null; }

expect_equal "rabbitmq: no MassTransit type or package in the project" "0" "$(grep -rl 'MassTransit' "$PROJECT/src" "$PROJECT/Directory.Packages.props" 2>/dev/null | wc -l | tr -d ' ')"

curl -s -o /dev/null -X DELETE "$API/exchanges/%2F/$(wire_name Orders LineAdded | sed 's/:/%3A/')"
start_worker
expect_equal "topology: the consumer's queue (named after it) and its error queue exist, as classic durable queues" "classic|True|classic|True" "$(broker "queues/%2F/$QUEUE" | python3 -I -c 'import sys, json; q = json.load(sys.stdin); print(q["arguments"].get("x-queue-type"), q["durable"], sep="|")')|$(broker "queues/%2F/${QUEUE}_error" | python3 -I -c 'import sys, json; q = json.load(sys.stdin); print(q["arguments"].get("x-queue-type"), q["durable"], sep="|")')"
expect_equal "topology: the event's exchange is a durable fanout bound to the consumer queue's exchange" "fanout|True|yes" "$(broker "exchanges/%2F/$(wire_name Orders LineAdded | sed 's/:/%3A/')" | python3 -I -c 'import sys, json; e = json.load(sys.stdin); print(e["type"], e["durable"], sep="|")')|$(broker "exchanges/%2F/$QUEUE/bindings/destination" | python3 -I -c 'import sys, json; print("yes" if any(b["source"].endswith(":LineAdded") for b in json.load(sys.stdin)) else "no")')"

CUSTOMER="$(id_of "$(run CreateCustomer "{\"name\":\"Rabbit\",\"email\":\"rabbit-$S@example.com\"}")")"
run CreateProduct "{\"sku\":\"RMQ-$S\",\"name\":\"Rabbit product\",\"stock\":10}" >/dev/null
run CreateOrder "{\"orderNumber\":\"RMQ-$S\",\"customerId\":$(jid "$CUSTOMER"),\"lines\":[{\"sku\":\"RMQ-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":3}]}" >/dev/null
expect_equal "delivery: the row went through the broker and the event consumer ran once" "7" "$(eventually "select stock from products where sku = 'RMQ-$S'" "7" 10)"

: > "$WORK/rabbitmq.log"
curl -s -X DELETE "$API/queues/%2F/${QUEUE}_error/contents" >/dev/null
run CreateOrder "{\"orderNumber\":\"RMF-$S\",\"customerId\":$(jid "$CUSTOMER"),\"lines\":[{\"sku\":\"RMQ-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":50}]}" >/dev/null
FAULT=""
for _ in $(seq 1 50); do
  FAULT="$(curl -s -X POST -H 'content-type: application/json' "$API/queues/%2F/${QUEUE}_error/get" -d '{"count":10,"ackmode":"ack_requeue_true","encoding":"auto"}' | python3 -I -c '
import sys, json
for m in json.load(sys.stdin):
    h = m["properties"].get("headers", {})
    if "RMQ-'"$S"'" in m["payload"] and "\"quantity\":50" in m["payload"].replace(" ", ""):
        print(h.get("MT-Reason"), h.get("MT-Fault-ExceptionType"), "yes" if h.get("MT-Fault-Message") else "no", sep="|")
        break')"
  [ -n "$FAULT" ] && break
  sleep 0.2
done
expect_equal "failure: a consumer that throws after its retries leaves the message in <queue>_error with the fault headers" "fault|Boilerplate.Shared.Exceptions.BusinessRuleException|yes" "$(printf '%s' "$FAULT" | sed "s/$NAME/Boilerplate/")"
expect_equal "failure: the queue keeps flowing (nothing is left unacknowledged)" "0" "$(sleep 1; broker "queues/%2F/$QUEUE" | python3 -I -c 'import sys, json; print(json.load(sys.stdin).get("messages_unacknowledged", 0))')"
stop_worker

WORKER_PID="$(ConnectionStrings__RabbitMq="amqp://guest:guest@localhost:5999/" "$LANG_SH" worker-start "$PROJECT" "$NAME")"
sleep 4
run CreateOrder "{\"orderNumber\":\"RMD-$S\",\"customerId\":$(jid "$CUSTOMER"),\"lines\":[{\"sku\":\"RMQ-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":2}]}" >/dev/null
expect_equal "broker down: the command still succeeded and its row waits, counted as a failed attempt" "yes|7" "$(eventually "select case when count(*) > 0 and min(attempts) >= 1 then 'yes' else 'no' end from outbox_messages where payload->>'Sku' = 'RMQ-$S' and (payload->>'Quantity')::int = 2" "yes" 10)|$(sql "select stock from products where sku = 'RMQ-$S'")"
stop_worker
sql "update outbox_messages set next_attempt_at = now() where payload->>'Sku' = 'RMQ-$S'" >/dev/null
start_worker
expect_equal "broker back: the waiting row was published and handled" "5|0" "$(eventually "select stock from products where sku = 'RMQ-$S'" "5" 15)|$(eventually "select count(*) from outbox_messages where payload->>'Sku' = 'RMQ-$S' and (payload->>'Quantity')::int = 2" "0" 10)"
stop_worker

report
[ "$FAILED" -eq 0 ]
