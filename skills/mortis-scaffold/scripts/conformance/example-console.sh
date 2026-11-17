#!/usr/bin/env bash
# Conformance: the example domain's Console scenarios and the command runner
# Needs: PROJECT NAME LANG_SH WORK DATABASE (and API_PORT for http.sh), a reachable PostgreSQL, psql, curl.
# Applies when: example = true, database = postgres; after example-database.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

# order-flow: the event handler's write commits with the command
FLOW_LOG="$(console_run order-flow 2>&1)"
expect_equal "order-flow: the order is completed" "Completed" "$(sql "select status from orders where order_number like 'ORD-%'")"
expect_equal "order-flow: the customer's count was raised by the event handler in the same transaction" "1" "$(sql "select completed_order_count from customers where email like 'customer-%'")"
expect_equal "order-flow: a child added to a loaded order was inserted (3 lines)" "3" "$(sql "select count(*) from order_lines l join orders o on o.id = l.order_id where o.order_number like 'ORD-%'")"
expect_equal "order-flow: the cancelled child was updated in place" "SKU-2|out of stock" "$(sql "select left(sku, 5) || '|' || cancellation_reason from order_lines where is_cancelled")"
expect_equal "order-flow: the value object is stored in two columns, currency upper-cased" "10.5000|EUR" "$(sql "select price_amount || '|' || price_currency from order_lines where left(sku, 5) = 'SKU-1' and price_amount = 10.5")"
expect_equal "order-flow: audit columns carry the console user" "console|console" "$(sql "select created_user_id || '|' || updated_user_id from orders where order_number like 'ORD-%'")"
expect_equal "order-flow: async handlers moved the stock after the commit (added 2, 1 and 4; cancelled 1)" "SKU-1=98,SKU-2=100,SKU-3=96" "$(sql "select string_agg(left(sku, 5) || '=' || stock, ',' order by sku) from products where sku like 'SKU-_-%'")"
expect_equal "order-flow: an async handler writes as the system user, the command as the console user" "system|console" "$(sql "select updated_user_id || '|' || created_user_id from products where sku like 'SKU-1-%'")"
case "$FLOW_LOG" in *"Published $(wire_name Orders Completed)"*) pass "order-flow: the queue event was published once the transaction committed" ;; *) fail "order-flow: the queue event was published once the transaction committed" ;; esac

# Sync and async handlers on the same event
STOCK_LOG="$(console_run stock-flow 2>&1)"
STOCK_JSON="$(printf '%s' "$STOCK_LOG" | json_tail)"
expect_equal "stock-flow: ordering bolt x3 and nut x2 lowered the stock (async, after commit)" "7|3" "$(json_field "$STOCK_JSON" "['AfterOrder']['Bolt']")|$(json_field "$STOCK_JSON" "['AfterOrder']['Nut']")"
expect_equal "stock-flow: cancelling one of two lines restored its stock and left the order open" "Open|10" "$(json_field "$STOCK_JSON" "['AfterFirstCancel']['OrderStatus']")|$(json_field "$STOCK_JSON" "['AfterFirstCancel']['Bolt']")"
expect_equal "stock-flow: cancelling the last line cancelled the order (sync) and restored the stock (async)" "Cancelled|5" "$(json_field "$STOCK_JSON" "['AfterLastCancel']['OrderStatus']")|$(json_field "$STOCK_JSON" "['AfterLastCancel']['Nut']")"
expect_equal "stock-flow: the database agrees: order cancelled, stock back to the start" "Cancelled|BOLT=10,NUT=5" "$(sql "select status from orders where order_number like 'STK-%'")|$(sql "select string_agg(split_part(sku, '-', 1) || '=' || stock, ',' order by sku) from products where sku like 'BOLT-%' or sku like 'NUT-%'")"
expect_equal "stock-flow: an oversold order stands, because an async handler cannot veto" "Open|10" "$(json_field "$STOCK_JSON" "['Oversell']['OrderStatus']")|$(json_field "$STOCK_JSON" "['Oversell']['Bolt']")"
case "$STOCK_LOG" in *"Async handler"*"failed"*) pass "stock-flow: the failed async handler was logged, not thrown" ;; *) fail "stock-flow: the failed async handler was logged, not thrown" ;; esac
expect_equal "stock-flow: the cancelled event of the order was published once the transaction committed" "1" "$(printf '%s' "$STOCK_LOG" | grep -c "Published $(wire_name Orders Cancelled)")"

# Any command from arguments (what F5 in VS Code runs)
CLI_OUT="$(console_run CreateProduct '{"sku":"CLI-1","name":"Cli","stock":5}' 2>/dev/null | json_tail)"
expect_equal "console: CreateProduct '<json>' prints the created product" "CLI-1|5" "$(json_field "$CLI_OUT" "['Sku']")|$(json_field "$CLI_OUT" "['Stock']")"
expect_equal "console: ... and the row is in the database, written by the console user" "5|console" "$(sql "select stock || '|' || created_user_id from products where sku = 'CLI-1'")"
console_run DecreaseProductStock '{"SKU":"CLI-1","Quantity":2}' >/dev/null 2>&1
expect_equal "console: a command name is matched ignoring case and the JSON names too" "3" "$(sql "select stock from products where sku = 'CLI-1'")"
printf '{"sku":"CLI-2","name":"From file","stock":1}' > "$WORK/product.json"
console_run createproduct "@$WORK/product.json" >/dev/null 2>&1
expect_equal "console: '@file.json' reads the command from a file" "1" "$(sql "select stock from products where sku = 'CLI-2'")"
console_run DecreaseProductStock '{"sku":"CLI-1","quantity":0}' >/dev/null 2>"$WORK/err.json"
CLI_CODE=$?
expect_equal "console: an invalid command exits with 2 and prints validation_failed on stderr" "2|validation_failed" "$CLI_CODE|$(python3 -c "import json; print(json.load(open('$WORK/err.json'))['code'])")"
expect_equal "console: ... and nothing was written" "3" "$(sql "select stock from products where sku = 'CLI-1'")"
console_run DecreaseProductStock '{not json' >/dev/null 2>"$WORK/err.json"
CLI_CODE=$?
expect_equal "console: broken JSON exits with 2 and prints invalid_parameter" "2|invalid_parameter" "$CLI_CODE|$(python3 -c "import json; print(json.load(open('$WORK/err.json'))['code'])")"
console_run NoSuchCommand >/dev/null 2>&1
expect_equal "console: an unknown name exits with 1" "1" "$?"

# Failure path: a throwing event handler rolls the whole command back
BLOCKED_LOG="$(console_run blocked-customer 2>&1)"
case "$BLOCKED_LOG" in *'"RejectedWith": "customer_blocked"'*) pass "blocked-customer: completing is rejected with customer_blocked" ;; *) fail "blocked-customer: completing is rejected with customer_blocked" ;; esac
expect_equal "blocked-customer: the order is still open in the database" "Open" "$(sql "select status from orders where order_number like 'BLK-%'")"
expect_equal "blocked-customer: the customer's count is unchanged" "0" "$(sql "select completed_order_count from customers where email like 'blocked-%'")"
case "$BLOCKED_LOG" in *"Orders.Events:Completed"*) fail "blocked-customer: the rolled-back Completed event is never published" ;; *) pass "blocked-customer: the rolled-back Completed event is never published" ;; esac

report
[ "$FAILED" -eq 0 ]
