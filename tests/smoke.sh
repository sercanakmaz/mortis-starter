#!/usr/bin/env bash
set -uo pipefail

# End-to-end check of the scaffold skill against a real PostgreSQL.
# Needs: dotnet SDK 10, psql, curl, and a PostgreSQL the PG* variables below can reach.

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCAFFOLD="$REPO_DIR/_temp/template/scripts/scaffold.sh"
WORK_DIR="${SMOKE_WORK_DIR:-$(mktemp -d)}"
export PGHOST="${PGHOST:-localhost}" PGPORT="${PGPORT:-5432}" PGUSER="${PGUSER:-postgres}" PGPASSWORD="${PGPASSWORD:-postgres}"
API_PORT="${SMOKE_API_PORT:-5099}"
DATABASE="smokeshop"
export ConnectionStrings__Default="Host=$PGHOST;Port=$PGPORT;Database=$DATABASE;Username=$PGUSER;Password=$PGPASSWORD"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

PASSED=0
FAILED=0

pass() { PASSED=$((PASSED + 1)); echo "PASS  $1"; }
fail() { FAILED=$((FAILED + 1)); echo "FAIL  $1"; [ $# -gt 1 ] && echo "      $2"; }

expect_equal() {
  if [ "$2" = "$3" ]; then pass "$1"; else fail "$1" "expected '$2', got '$3'"; fi
}

sql() { psql -d "$DATABASE" -tAc "$1"; }

echo "Working in $WORK_DIR"

# 0. The skill's reference docs describe the template and the example as they are
DOCS_RESULT="$(python3 -I "$REPO_DIR/tests/check_docs.py" 2>&1)"
case "$DOCS_RESULT" in
  "docs match the template and the example") pass "reference docs match the template and the example" ;;
  *) fail "reference docs match the template and the example" "$DOCS_RESULT" ;;
esac

# 1. Core scaffold builds and its tests pass
"$SCAFFOLD" SmokeCore --no-example --out "$WORK_DIR" >/dev/null
(cd "$WORK_DIR/SmokeCore" && dotnet build >build.log 2>&1 || true)
if grep -q "0 Warning(s)" "$WORK_DIR/SmokeCore/build.log" && grep -q "0 Error(s)" "$WORK_DIR/SmokeCore/build.log"; then
  pass "core scaffold builds with 0 warnings and 0 errors"
else
  fail "core scaffold builds with 0 warnings and 0 errors" "see $WORK_DIR/SmokeCore/build.log"
fi
CORE_TESTS="$(cd "$WORK_DIR/SmokeCore" && dotnet test --no-build 2>&1 | grep -E "^(Passed|Failed)!" || true)"
case "$CORE_TESTS" in Passed!*) pass "core scaffold tests: $CORE_TESTS" ;; *) fail "core scaffold tests" "$CORE_TESTS" ;; esac

CORE_RESIDUE="$(grep -rIlE "Customer|Product|Orders\b|Order(Line|Status|Number|Service|Dto|Constants|Module)|StockFlow" "$WORK_DIR/SmokeCore/src" "$WORK_DIR/SmokeCore/test" --include=*.cs --include=*.json --exclude-dir=bin --exclude-dir=obj 2>/dev/null | head -3)"
[ -z "$CORE_RESIDUE" ] && pass "--no-example leaves no example code behind" || fail "--no-example leaves no example code behind" "$CORE_RESIDUE"
[ ! -e "$WORK_DIR/SmokeCore/src/SmokeCore.Infrastructure/Persistence/Migrations" ] && pass "--no-example leaves no migration behind" || fail "--no-example leaves no migration behind"

# 2. Sample scaffold builds and its tests pass
"$SCAFFOLD" SmokeShop --out "$WORK_DIR" >/dev/null
SHOP="$WORK_DIR/SmokeShop"
(cd "$SHOP" && dotnet build >build.log 2>&1 || true)
if grep -q "0 Warning(s)" "$SHOP/build.log" && grep -q "0 Error(s)" "$SHOP/build.log"; then
  pass "example scaffold builds with 0 warnings and 0 errors"
else
  fail "example scaffold builds with 0 warnings and 0 errors" "see $SHOP/build.log"
fi
SHOP_TESTS="$(cd "$SHOP" && dotnet test --no-build 2>&1 | grep -E "^(Passed|Failed)!" || true)"
case "$SHOP_TESTS" in Passed!*) pass "example scaffold tests: $SHOP_TESTS" ;; *) fail "example scaffold tests" "$SHOP_TESTS" ;; esac

if grep -rIl --exclude-dir=bin --exclude-dir=obj -i "boilerplate" "$SHOP" >/dev/null 2>&1; then
  fail "no placeholder name is left in the scaffolded solution" "$(grep -rIl --exclude-dir=bin --exclude-dir=obj -i boilerplate "$SHOP" | head -3)"
else
  pass "no placeholder name is left in the scaffolded solution"
fi

# 3. The banned-API guard fails the build
PLANTED="$SHOP/src/SmokeShop.Domain/Planted.cs"
printf 'namespace SmokeShop.Domain;\n\npublic static class Planted\n{\n    public static System.Guid Id() => System.Guid.NewGuid();\n}\n' > "$PLANTED"
PLANTED_BUILD="$(cd "$SHOP" && dotnet build src/SmokeShop.Domain 2>&1 || true)"
case "$PLANTED_BUILD" in
  *"error RS0030"*) pass "Guid.NewGuid() fails the build with RS0030" ;;
  *) fail "Guid.NewGuid() fails the build with RS0030" ;;
esac
rm -f "$PLANTED"
(cd "$SHOP" && dotnet build >/dev/null 2>&1)

# 4. Migrations apply to a fresh database
psql -d postgres -qc "DROP DATABASE IF EXISTS $DATABASE" -c "CREATE DATABASE $DATABASE" >/dev/null 2>&1
CONSOLE="dotnet run --no-build --project $SHOP/src/SmokeShop.Console --"
MIGRATE_LOG="$(cd "$SHOP" && dotnet ef database update --no-build --project src/SmokeShop.Infrastructure 2>&1 || true)"
case "$MIGRATE_LOG" in *"Done."*) pass "dotnet ef database update applies the migrations" ;; *) fail "dotnet ef database update applies the migrations: $MIGRATE_LOG" ;; esac
expect_equal "tables exist with snake_case names" "customers,order_lines,orders,products" "$(sql "select string_agg(table_name, ',' order by table_name) from information_schema.tables where table_schema = 'public' and table_name <> '__EFMigrationsHistory'")"
expect_equal "orders.customer_id is a real foreign key" "1" "$(sql "select count(*) from information_schema.table_constraints where table_name = 'orders' and constraint_type = 'FOREIGN KEY'")"
expect_equal "order_lines references both orders and products by foreign key" "2" "$(sql "select count(*) from information_schema.table_constraints where table_name = 'order_lines' and constraint_type = 'FOREIGN KEY'")"
expect_equal "products.stock cannot go negative in the database either" "1" "$(sql "select count(*) from information_schema.table_constraints where table_name = 'products' and constraint_name = 'ck_products_stock_not_negative'")"

# 5. Success path: the event handler's write commits with the command
FLOW_LOG="$($CONSOLE order-flow 2>&1)"
expect_equal "order-flow: the order is completed" "Completed" "$(sql "select status from orders where order_number like 'ORD-%'")"
expect_equal "order-flow: the customer's count was raised by the event handler in the same transaction" "1" "$(sql "select completed_order_count from customers where email like 'customer-%'")"
expect_equal "order-flow: a child added to a loaded order was inserted (3 lines)" "3" "$(sql "select count(*) from order_lines l join orders o on o.id = l.order_id where o.order_number like 'ORD-%'")"
expect_equal "order-flow: the cancelled child was updated in place" "SKU-2|out of stock" "$(sql "select left(sku, 5) || '|' || cancellation_reason from order_lines where is_cancelled")"
expect_equal "order-flow: the value object is stored in two columns, currency upper-cased" "10.5000|EUR" "$(sql "select price_amount || '|' || price_currency from order_lines where left(sku, 5) = 'SKU-1' and price_amount = 10.5")"
expect_equal "order-flow: audit columns carry the console user" "console|console" "$(sql "select created_user_id || '|' || updated_user_id from orders where order_number like 'ORD-%'")"
expect_equal "order-flow: async handlers moved the stock after the commit (added 2, 1 and 4; cancelled 1)" "SKU-1=98,SKU-2=100,SKU-3=96" "$(sql "select string_agg(left(sku, 5) || '=' || stock, ',' order by sku) from products where sku like 'SKU-_-%'")"
expect_equal "order-flow: an async handler writes as the system user, the command as the console user" "system|console" "$(sql "select updated_user_id || '|' || created_user_id from products where sku like 'SKU-1-%'")"
case "$FLOW_LOG" in *"Published SmokeShop.Domain.Aggregates.Orders.Events:Completed"*) pass "order-flow: the queue event was published once the transaction committed" ;; *) fail "order-flow: the queue event was published once the transaction committed" ;; esac

# 5b. Sync and async handlers on the same event
STOCK_LOG="$($CONSOLE stock-flow 2>&1)"
STOCK_JSON="$(printf '%s' "$STOCK_LOG" | python3 -c "import sys; s = sys.stdin.read(); print(s[s.index(chr(10) + '{') + 1:])")"
json_field() { python3 -c "import sys, json; d = json.loads(sys.argv[1]); print(d$2)" "$1"; }
expect_equal "stock-flow: ordering bolt x3 and nut x2 lowered the stock (async, after commit)" "7|3" "$(json_field "$STOCK_JSON" "['AfterOrder']['Bolt']")|$(json_field "$STOCK_JSON" "['AfterOrder']['Nut']")"
expect_equal "stock-flow: cancelling one of two lines restored its stock and left the order open" "Open|10" "$(json_field "$STOCK_JSON" "['AfterFirstCancel']['OrderStatus']")|$(json_field "$STOCK_JSON" "['AfterFirstCancel']['Bolt']")"
expect_equal "stock-flow: cancelling the last line cancelled the order (sync) and restored the stock (async)" "Cancelled|5" "$(json_field "$STOCK_JSON" "['AfterLastCancel']['OrderStatus']")|$(json_field "$STOCK_JSON" "['AfterLastCancel']['Nut']")"
expect_equal "stock-flow: the database agrees: order cancelled, stock back to the start" "Cancelled|BOLT=10,NUT=5" "$(sql "select status from orders where order_number like 'STK-%'")|$(sql "select string_agg(split_part(sku, '-', 1) || '=' || stock, ',' order by sku) from products where sku like 'BOLT-%' or sku like 'NUT-%'")"
expect_equal "stock-flow: an oversold order stands, because an async handler cannot veto" "Open|10" "$(json_field "$STOCK_JSON" "['Oversell']['OrderStatus']")|$(json_field "$STOCK_JSON" "['Oversell']['Bolt']")"
case "$STOCK_LOG" in *"Async handler DecreaseStockWhenOrderLineAddedEventHandler failed"*) pass "stock-flow: the failed async handler was logged, not thrown" ;; *) fail "stock-flow: the failed async handler was logged, not thrown" ;; esac
expect_equal "stock-flow: the cancelled event of the order was published once the transaction committed" "1" "$(printf '%s' "$STOCK_LOG" | grep -c 'Published SmokeShop.Domain.Aggregates.Orders.Events:Cancelled')"

# 5c. Any command from arguments (what F5 in VS Code runs)
CLI_OUT="$($CONSOLE CreateProduct '{"sku":"CLI-1","name":"Cli","stock":5}' 2>/dev/null | python3 -c "import sys; s = sys.stdin.read(); print(s[s.index(chr(10) + '{') + 1:])")"
expect_equal "console: CreateProduct '<json>' prints the created product" "CLI-1|5" "$(json_field "$CLI_OUT" "['Sku']")|$(json_field "$CLI_OUT" "['Stock']")"
expect_equal "console: ... and the row is in the database, written by the console user" "5|console" "$(sql "select stock || '|' || created_user_id from products where sku = 'CLI-1'")"
$CONSOLE DecreaseProductStock '{"SKU":"CLI-1","Quantity":2}' >/dev/null 2>&1
expect_equal "console: a command name is matched ignoring case and the JSON names too" "3" "$(sql "select stock from products where sku = 'CLI-1'")"
printf '{"sku":"CLI-2","name":"From file","stock":1}' > "$WORK_DIR/product.json"
$CONSOLE createproduct "@$WORK_DIR/product.json" >/dev/null 2>&1
expect_equal "console: '@file.json' reads the command from a file" "1" "$(sql "select stock from products where sku = 'CLI-2'")"
$CONSOLE DecreaseProductStock '{"sku":"CLI-1","quantity":0}' >/dev/null 2>"$WORK_DIR/err.json"
CLI_CODE=$?
expect_equal "console: an invalid command exits with 2 and prints validation_failed on stderr" "2|validation_failed" "$CLI_CODE|$(python3 -c "import json; print(json.load(open('$WORK_DIR/err.json'))['code'])")"
expect_equal "console: ... and nothing was written" "3" "$(sql "select stock from products where sku = 'CLI-1'")"
$CONSOLE DecreaseProductStock '{not json' >/dev/null 2>"$WORK_DIR/err.json"
CLI_CODE=$?
expect_equal "console: broken JSON exits with 2 and prints invalid_parameter" "2|invalid_parameter" "$CLI_CODE|$(python3 -c "import json; print(json.load(open('$WORK_DIR/err.json'))['code'])")"
$CONSOLE NoSuchCommand >/dev/null 2>&1
expect_equal "console: an unknown name exits with 1" "1" "$?"
expect_equal "vscode: launch.json is valid JSON, prompts for the arguments and builds first" "True" "$(python3 -c "import json; d = json.load(open('$SHOP/.vscode/launch.json')); print(any('\${input:name}' in c['args'] for c in d['configurations']) and all(c['preLaunchTask'] == 'build-console' for c in d['configurations']))")"
expect_equal "vscode: launch.json points at the renamed console assembly" "True" "$(python3 -c "import json; d = json.load(open('$SHOP/.vscode/launch.json')); print(all('SmokeShop.Console.dll' in c['program'] for c in d['configurations']))")"

# 6. Failure path: a throwing event handler rolls the whole command back
BLOCKED_LOG="$($CONSOLE blocked-customer 2>&1)"
case "$BLOCKED_LOG" in *'"RejectedWith": "customer_blocked"'*) pass "blocked-customer: completing is rejected with customer_blocked" ;; *) fail "blocked-customer: completing is rejected with customer_blocked" ;; esac
expect_equal "blocked-customer: the order is still open in the database" "Open" "$(sql "select status from orders where order_number like 'BLK-%'")"
expect_equal "blocked-customer: the customer's count is unchanged" "0" "$(sql "select completed_order_count from customers where email like 'blocked-%'")"
case "$BLOCKED_LOG" in *"Orders.Events:Completed"*) fail "blocked-customer: the rolled-back Completed event is never published" ;; *) pass "blocked-customer: the rolled-back Completed event is never published" ;; esac

# 7. HTTP host: status codes and error bodies
(cd "$SHOP/src/SmokeShop.Api" && ASPNETCORE_URLS="http://localhost:$API_PORT" dotnet run --no-build >"$WORK_DIR/api.log" 2>&1) &
API_PID=$!
for _ in $(seq 1 60); do curl -fs "http://localhost:$API_PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done

request() { curl -s -o "$WORK_DIR/body.json" -w "%{http_code}" -X "$1" "http://localhost:$API_PORT$2" -H "Content-Type: application/json" ${3:+-d "$3"}; }
body_field() { python3 -c "import json,sys; d=json.load(open('$WORK_DIR/body.json')); print(d$1)"; }

expect_equal "api: POST /products creates a product" "201" "$(request POST /products '{"sku":"S","name":"Small","stock":100}')"
expect_equal "api: the same sku again is a 409" "409" "$(request POST /products '{"sku":"S","name":"Small","stock":100}')"
expect_equal "api: POST /customers creates a customer" "201" "$(request POST /customers '{"name":"Ada","email":"Ada@Example.com"}')"
CUSTOMER_ID="$(body_field "['id']")"
expect_equal "api: the email was normalized by the aggregate" "ada@example.com" "$(body_field "['email']")"
expect_equal "api: the same email again is a 409" "409" "$(request POST /customers '{"name":"Ada","email":"ada@example.com"}')"
expect_equal "api: ... with the code 'duplicate'" "duplicate" "$(body_field "['code']")"
expect_equal "api: an order without lines is a 422" "422" "$(request POST /orders "{\"orderNumber\":\"API-1\",\"customerId\":\"$CUSTOMER_ID\",\"lines\":[]}")"
expect_equal "api: ... that names the failing field" "Lines" "$(body_field "['errors'].__iter__().__next__()")"
expect_equal "api: an order for a missing customer is rejected by the foreign key" "400" "$(request POST /orders '{"orderNumber":"API-2","customerId":"01900000-0000-7000-8000-000000000000","lines":[{"sku":"S","price":1,"currency":"EUR","quantity":1}]}')"
expect_equal "api: ... with the code 'reference_violation'" "reference_violation" "$(body_field "['code']")"
expect_equal "api: ... and no order row was left behind" "0" "$(sql "select count(*) from orders where order_number = 'API-2'")"
expect_equal "api: an order line with an unknown sku is rejected by the foreign key" "400" "$(request POST /orders "{\"orderNumber\":\"API-4\",\"customerId\":\"$CUSTOMER_ID\",\"lines\":[{\"sku\":\"NO-SUCH-SKU\",\"price\":1,\"currency\":\"EUR\",\"quantity\":1}]}")"
expect_equal "api: ... with the code 'reference_violation'" "reference_violation" "$(body_field "['code']")"
expect_equal "api: a valid order is created" "201" "$(request POST /orders "{\"orderNumber\":\"API-3\",\"customerId\":\"$CUSTOMER_ID\",\"lines\":[{\"sku\":\"S\",\"price\":2.5,\"currency\":\"eur\",\"quantity\":2}]}")"
ORDER_ID="$(body_field "['id']")"
expect_equal "api: PUT /orders/{id}/complete completes it" "200" "$(request PUT "/orders/$ORDER_ID/complete")"
expect_equal "api: completing it twice is a 409 business rule" "409|order_not_open" "$(request PUT "/orders/$ORDER_ID/complete")|$(body_field "['code']")"
expect_equal "api: GET of an unknown order is a 404" "404" "$(request GET /orders/01900000-0000-7000-8000-000000000001)"

request POST /orders "{\"orderNumber\":\"API-5\",\"customerId\":\"$CUSTOMER_ID\",\"lines\":[{\"sku\":\"S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":1}]}" >/dev/null
CANCEL_ORDER_ID="$(body_field "['id']")"
CANCEL_LINE_ID="$(body_field "['lines'][0]['id']")"
expect_equal "api: cancelling the only line answers 200" "200" "$(request PUT "/orders/$CANCEL_ORDER_ID/lines/$CANCEL_LINE_ID/cancel" '{"reason":"changed mind"}')"
request GET "/orders/$CANCEL_ORDER_ID" >/dev/null
expect_equal "api: reading it back, the sync handler has cancelled the order" "Cancelled" "$(body_field "['status']")"
expect_equal "api: and the async handler gave the unit back (100 - 2 - 1 + 1)" "98" "$(sql "select stock from products where sku = 'S'")"

request POST /products '{"sku":"HOT","name":"Hot item","stock":8}' >/dev/null
CONCURRENT_PIDS=()
for index in $(seq 1 8); do
  curl -s -o /dev/null -X POST "http://localhost:$API_PORT/orders" -H "Content-Type: application/json" \
    -d "{\"orderNumber\":\"CON-$index\",\"customerId\":\"$CUSTOMER_ID\",\"lines\":[{\"sku\":\"HOT\",\"price\":1,\"currency\":\"EUR\",\"quantity\":1}]}" &
  CONCURRENT_PIDS+=($!)
done
wait "${CONCURRENT_PIDS[@]}"
expect_equal "api: 8 concurrent orders of one product were all accepted" "8" "$(sql "select count(*) from orders where order_number like 'CON-%'")"
expect_equal "api: no stock decrement was lost to a race (8 - 8)" "0" "$(sql "select stock from products where sku = 'HOT'")"

kill "$API_PID" 2>/dev/null
pkill -f "SmokeShop.Api" 2>/dev/null
wait "$API_PID" 2>/dev/null

# 8. Kernel-only mode
"$SCAFFOLD" SmokeKernel --kernel-only --out "$WORK_DIR" >/dev/null
expect_equal "kernel-only: only the Shared project and the build guards are copied" ".editorconfig.example,BannedSymbols.txt,Directory.Build.props.example,Directory.Packages.props.example,src" "$(ls -A "$WORK_DIR/SmokeKernel" | sort | tr '\n' ',' | sed 's/,$//')"

echo
echo "$PASSED passed, $FAILED failed"
[ "$FAILED" -eq 0 ]
