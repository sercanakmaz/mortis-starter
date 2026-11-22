#!/usr/bin/env bash
# Conformance: the example domain driven one Console command at a time, its events and its JSON log lines.
# Needs: PROJECT NAME LANG_SH WORK DATABASE API_PORT OUTBOX WORKER, a migrated PostgreSQL, psql, curl.
# Applies when: example = true, database = postgres; after example-database.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
# The user every Console run names with --user (lib.sh).
CONSOLE_USER="${CONSOLE_USER:-console}"

OUTBOX="${OUTBOX:-none}"
WORKER="${WORKER:-in-api}"
S="$(python3 -I -c 'import uuid; print(uuid.uuid4().hex[:8])')"
RELAY_PID=""

if [ "$OUTBOX" = "background" ]; then
  if [ "$WORKER" = "separate" ]; then
    RELAY_PID="$("$LANG_SH" worker-start "$PROJECT" "$NAME")"
    RELAY_LOG="$PROJECT/.mortis/worker.log"
    sleep 4
  else
    RELAY_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$((API_PORT + 3))")"
    RELAY_LOG="$PROJECT/.mortis/api.log"
    for _ in $(seq 1 60); do curl -fs "http://localhost:$((API_PORT + 3))/health" >/dev/null 2>&1 && break; sleep 0.5; done
  fi
fi

run() { console_run "$@" 2>>"$WORK/console.log"; }
id_of() { json_field "$1" "['Id']"; }
events_log() { if [ -n "$RELAY_PID" ]; then printf '%s' "$RELAY_LOG"; else printf '%s' "$WORK/console.log"; fi; }
: > "$WORK/console.log"

# Any command, by name, from its JSON
CLI_OUT="$(run CreateProduct "{\"sku\":\"CLI-1-$S\",\"name\":\"Cli\",\"stock\":5}")"
expect_equal "console: CreateProduct '<json>' prints only the created product on stdout" "CLI-1-$S|5" "$(json_field "$CLI_OUT" "['Sku']")|$(json_field "$CLI_OUT" "['Stock']")"
expect_equal "console: ... and the row is in the database, written by the user named with --user" "5|$CONSOLE_USER" "$(sql "select stock || '|' || created_user_id from products where sku = 'CLI-1-$S'")"
run decreaseproductstock "{\"SKU\":\"CLI-1-$S\",\"Quantity\":2}" >/dev/null
expect_equal "console: a command name is matched ignoring case and the JSON names too" "3" "$(sql "select stock from products where sku = 'CLI-1-$S'")"
printf '{"sku":"CLI-2-%s","name":"From file","stock":1}' "$S" > "$WORK/product.json"
run CreateProduct "@$WORK/product.json" >/dev/null
expect_equal "console: '@file.json' reads the command from a file" "1" "$(sql "select stock from products where sku = 'CLI-2-$S'")"
console_run DecreaseProductStock "{\"sku\":\"CLI-1-$S\",\"quantity\":0}" >/dev/null 2>"$WORK/err.log"
expect_equal "console: an invalid command exits with 2 and prints validation_failed on stderr" "2|validation_failed" "$?|$(error_code "$WORK/err.log")"
expect_equal "console: ... and nothing was written" "3" "$(sql "select stock from products where sku = 'CLI-1-$S'")"
console_run DecreaseProductStock '{not json' >/dev/null 2>"$WORK/err.log"
expect_equal "console: broken JSON exits with 2 and prints invalid_parameter" "2|invalid_parameter" "$?|$(error_code "$WORK/err.log")"
"$LANG_SH" console "$PROJECT" "$NAME" CreateProduct "{\"sku\":\"NOUSER-$S\",\"name\":\"Nobody\",\"stock\":1}" >/dev/null 2>"$WORK/err.log"
expect_equal "console: a run without --user is refused with invalid_parameter (exit 2), and nothing is written" "2|invalid_parameter|0" "$?|$(error_code "$WORK/err.log")|$(sql "select count(*) from products where sku = 'NOUSER-$S'")"
expect_equal "logs: every line on stderr is one flat JSON object with CorrelationId first" "yes" "$(python3 -I -c '
import json, sys
lines = [line for line in open(sys.argv[1]) if line.strip()]
logs = [json.loads(line) for line in lines]
flat = all(not isinstance(v, (dict, list)) for l in logs for v in l.values())
first = all(next(iter(l)) == "CorrelationId" for l in logs if "code" not in l)
print("yes" if logs and flat and first else "no")' "$WORK/console.log")"
expect_equal "logs: the command's lines carry its correlation, user and name as fields" "yes" "$([ "$(log_count "$WORK/console.log" "l.get('Command') == 'CreateProductCommand' and l.get('UserId') == '$CONSOLE_USER' and l.get('CorrelationId') not in (None, '') and l.get('Message') == 'CreateProductCommand started'")" -ge 1 ] && echo yes || echo no)"

# A whole order, one command at a time
P1="$(id_of "$(run CreateProduct "{\"sku\":\"SKU-1-$S\",\"name\":\"First\",\"stock\":100}")")"
run CreateProduct "{\"sku\":\"SKU-2-$S\",\"name\":\"Second\",\"stock\":100}" >/dev/null
run CreateProduct "{\"sku\":\"SKU-3-$S\",\"name\":\"Third\",\"stock\":100}" >/dev/null
CUSTOMER="$(id_of "$(run CreateCustomer "{\"name\":\"Console Customer\",\"email\":\"customer-$S@example.com\"}")")"
ORDER="$(id_of "$(run CreateOrder "{\"orderNumber\":\"ORD-$S\",\"customerId\":\"$CUSTOMER\",\"lines\":[{\"sku\":\"SKU-1-$S\",\"price\":10.50,\"currency\":\"eur\",\"quantity\":2},{\"sku\":\"SKU-2-$S\",\"price\":4.00,\"currency\":\"EUR\",\"quantity\":1}]}")")"
WITH_LINE="$(run AddOrderLine "{\"id\":\"$ORDER\",\"line\":{\"sku\":\"SKU-3-$S\",\"price\":1.25,\"currency\":\"EUR\",\"quantity\":4}}")"
LINE2="$(json_field "$WITH_LINE" "['Lines'][[l['Sku'] for l in d['Lines']].index('SKU-2-$S')]['Id']")"
run CancelOrderLine "{\"id\":\"$ORDER\",\"lineId\":\"$LINE2\",\"reason\":\"out of stock\"}" >/dev/null
: > "$WORK/console.log"
run CompleteOrder "{\"id\":\"$ORDER\"}" >/dev/null
COMPLETE_CORRELATION="$(log_field "$WORK/console.log" "l.get('Message') == 'CompleteOrderCommand started'" CorrelationId)"
expect_equal "order: the order is completed" "Completed" "$(sql "select status from orders where order_number = 'ORD-$S'")"
expect_equal "order: the customer's count was raised by the sync event handler in the same transaction" "1" "$(sql "select completed_order_count from customers where email = 'customer-$S@example.com'")"
expect_equal "order: a child added to a loaded order was inserted (3 lines)" "3" "$(sql "select count(*) from order_lines l join orders o on o.id = l.order_id where o.order_number = 'ORD-$S'")"
expect_equal "order: the cancelled child was updated in place" "out of stock" "$(sql "select cancellation_reason from order_lines where is_cancelled and sku = 'SKU-2-$S'")"
expect_equal "order: the value object is stored in two columns, currency upper-cased" "10.5000|EUR" "$(sql "select price_amount || '|' || price_currency from order_lines where sku = 'SKU-1-$S'")"
expect_equal "order: audit columns carry the user named with --user" "$CONSOLE_USER|$CONSOLE_USER" "$(sql "select created_user_id || '|' || updated_user_id from orders where order_number = 'ORD-$S'")"
expect_equal "order: event consumers moved the stock after the commit (added 2, 1 and 4; cancelled 1)" "SKU-1=98,SKU-2=100,SKU-3=96" "$(eventually "select string_agg(left(sku, 5) || '=' || stock, ',' order by sku) from products where sku like 'SKU-_-$S'" "SKU-1=98,SKU-2=100,SKU-3=96")"
expect_equal "order: an event consumer writes as the user whose command raised the event; there is no system user" "$CONSOLE_USER|$CONSOLE_USER" "$(sql "select updated_user_id || '|' || created_user_id from products where id = '$P1'")"
expect_equal "order: the sync handler's command carries the Complete command's correlation, caused by its event" "1" "$(log_count "$WORK/console.log" "l.get('Message') == 'RegisterCompletedOrderCommand started' and l.get('CorrelationId') == '$COMPLETE_CORRELATION' and l.get('CausationId') not in (None, '')")"

# Sync and event consumers on one event, and the causation chain
run CreateProduct "{\"sku\":\"BOLT-$S\",\"name\":\"Bolt\",\"stock\":10}" >/dev/null
run CreateProduct "{\"sku\":\"NUT-$S\",\"name\":\"Nut\",\"stock\":5}" >/dev/null
STOCK_CUSTOMER="$(id_of "$(run CreateCustomer "{\"name\":\"Stock Customer\",\"email\":\"stock-$S@example.com\"}")")"
STOCK_ORDER_JSON="$(run CreateOrder "{\"orderNumber\":\"STK-$S\",\"customerId\":\"$STOCK_CUSTOMER\",\"lines\":[{\"sku\":\"BOLT-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":3},{\"sku\":\"NUT-$S\",\"price\":0.5,\"currency\":\"EUR\",\"quantity\":2}]}")"
STOCK_ORDER="$(id_of "$STOCK_ORDER_JSON")"
BOLT_LINE="$(json_field "$STOCK_ORDER_JSON" "['Lines'][[l['Sku'] for l in d['Lines']].index('BOLT-$S')]['Id']")"
NUT_LINE="$(json_field "$STOCK_ORDER_JSON" "['Lines'][[l['Sku'] for l in d['Lines']].index('NUT-$S')]['Id']")"
expect_equal "stock: ordering bolt x3 and nut x2 lowered the stock (async, after commit)" "7|3" "$(eventually "select (select stock from products where sku = 'BOLT-$S') || '|' || (select stock from products where sku = 'NUT-$S')" "7|3")"
run CancelOrderLine "{\"id\":\"$STOCK_ORDER\",\"lineId\":\"$BOLT_LINE\",\"reason\":\"changed mind\"}" >/dev/null
expect_equal "stock: cancelling one of two lines restored its stock and left the order open" "Open|10" "$(eventually "select status || '|' || (select stock from products where sku = 'BOLT-$S') from orders where id = '$STOCK_ORDER'" "Open|10")"
: > "$WORK/console.log"
run CancelOrderLine "{\"id\":\"$STOCK_ORDER\",\"lineId\":\"$NUT_LINE\",\"reason\":\"changed mind\"}" >/dev/null
LAST_CORRELATION="$(log_field "$WORK/console.log" "l.get('Message') == 'CancelOrderLineCommand started'" CorrelationId)"
expect_equal "stock: cancelling the last line cancelled the order (sync) and restored the stock (async)" "Cancelled|5" "$(eventually "select status || '|' || (select stock from products where sku = 'NUT-$S') from orders where id = '$STOCK_ORDER'" "Cancelled|5")"
for _ in $(seq 1 30); do
CHAIN="$(python3 -I - "$WORK/console.log" "$(events_log)" "$LAST_CORRELATION" <<'PY'
import json, sys
logs = [json.loads(line) for path in sys.argv[1:3] for line in open(path) if line.startswith("{")]
started = lambda name: [l for l in logs if l.get("Message") == name + " started" and l.get("CorrelationId") == sys.argv[3]]
sync = started("CancelOrderWhenAllLinesCancelledCommand")
asynchronous = started("IncreaseProductStockCommand")
print("|".join(["yes" if sync and sync[0].get("CausationId") else "no",
                "yes" if sync and asynchronous and asynchronous[0].get("CausationId") == sync[0].get("CausationId") else "no"]))
PY
)"
[ "$CHAIN" = "yes|yes" ] && break
sleep 0.2
done
expect_equal "stock: the sync handler's command carries the correlation of the cancel, caused by the LineCancelled event" "yes" "$(printf '%s' "$CHAIN" | cut -d'|' -f1)"
expect_equal "stock: the event consumer's command carries the same correlation and the same cause, in every publishing model" "yes" "$(printf '%s' "$CHAIN" | cut -d'|' -f2)"
: > "$WORK/console.log"
OVERSOLD="$(id_of "$(run CreateOrder "{\"orderNumber\":\"OVR-$S\",\"customerId\":\"$STOCK_CUSTOMER\",\"lines\":[{\"sku\":\"BOLT-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":20}]}")")"
sleep 1
expect_equal "stock: an oversold order stands, because an event consumer cannot veto" "Open|10" "$(sql "select status || '|' || (select stock from products where sku = 'BOLT-$S') from orders where id = '$OVERSOLD'")"
expect_equal "stock: the failed event consumer was logged as a JSON line with the exception, not thrown" "yes" "$(n="$(log_count "$(events_log)" "'failed' in l.get('Message', '') and 'Exception' in l")"; [ "$n" -ge 1 ] && echo yes || echo no)"

# Failure path: a throwing sync handler rolls the whole command back
BLOCKED_CUSTOMER="$(id_of "$(run CreateCustomer "{\"name\":\"Blocked\",\"email\":\"blocked-$S@example.com\"}")")"
run CreateProduct "{\"sku\":\"BLK-$S\",\"name\":\"Blocked product\",\"stock\":10}" >/dev/null
BLOCKED_ORDER="$(id_of "$(run CreateOrder "{\"orderNumber\":\"BLK-$S\",\"customerId\":\"$BLOCKED_CUSTOMER\",\"lines\":[{\"sku\":\"BLK-$S\",\"price\":25,\"currency\":\"EUR\",\"quantity\":1}]}")")"
run BlockCustomer "{\"id\":\"$BLOCKED_CUSTOMER\"}" >/dev/null
: > "$WORK/console.log"
console_run CompleteOrder "{\"id\":\"$BLOCKED_ORDER\"}" >/dev/null 2>"$WORK/console.log"
expect_equal "blocked-customer: completing is rejected with customer_blocked (exit 2)" "2|customer_blocked" "$?|$(error_code "$WORK/console.log")"
expect_equal "blocked-customer: the order is still open in the database" "Open" "$(sql "select status from orders where id = '$BLOCKED_ORDER'")"
expect_equal "blocked-customer: the customer's count is unchanged" "0" "$(sql "select completed_order_count from customers where id = '$BLOCKED_CUSTOMER'")"
expect_equal "blocked-customer: the rolled-back Completed event is never published" "0" "$(log_count "$WORK/console.log" "l.get('Event') == 'Completed'")"

[ -n "$RELAY_PID" ] && { kill "$RELAY_PID" 2>/dev/null; wait "$RELAY_PID" 2>/dev/null; }

report
[ "$FAILED" -eq 0 ]
