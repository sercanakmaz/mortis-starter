#!/usr/bin/env bash
# Phase 3 step 03, the shared outbox: a C# Api (events.outbox = background, hosts.worker = separate, so it only writes rows)
# and the Go worker of _temp/interop/go-worker share one PostgreSQL database and one outbox table. An order created through
# the C# Api must move the stock through the Go event consumers, as the user who created it, and leave no row behind.
# Needs PostgreSQL (PG* variables) and .NET and Go on the PATH. Usage: bash tests/check_interop_outbox.sh
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/_temp/variants-extra/standard-w"
GO_WORKER="$ROOT/_temp/interop/go-worker"
LANG_SH="$ROOT/skills/mortis-scaffold/scripts/lang/csharp.sh"
NAME=Boilerplate
PORT="${MORTIS_INTEROP_PORT:-5600}"
DATABASE=mortis_interop_outbox
WORK="$(mktemp -d)"
export ConnectionStrings__Default="Host=${PGHOST:-localhost};Port=${PGPORT:-5432};Database=$DATABASE;Username=${PGUSER:-postgres};Password=${PGPASSWORD:-postgres}"
PASSED=0
FAILED=0
API_PID=""
GO_PID=""

stop() { for pid in $API_PID $GO_PID; do kill "$pid" 2>/dev/null; wait "$pid" 2>/dev/null; done; }
trap stop EXIT
check() { if [ "$2" = "$3" ]; then echo "PASS  $1"; PASSED=$((PASSED + 1)); else echo "FAIL  $1"; echo "      expected '$2', got '$3'"; FAILED=$((FAILED + 1)); fi; }
sql() { psql -d "$DATABASE" -tAc "$1"; }
eventually() { local query="$1" expected="$2" value=""; for _ in $(seq 1 60); do value="$(sql "$query")"; [ "$value" = "$expected" ] && break; sleep 0.5; done; printf '%s' "$value"; }
call() { curl -s -o "$WORK/body.json" -w "%{http_code}" -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" "$@"; }
field() { python3 -I -c "import json,sys; print(json.load(open(sys.argv[1]))[sys.argv[2]])" "$WORK/body.json" "$1"; }

psql -d postgres -tAc "drop database if exists $DATABASE with (force)" >/dev/null
psql -d postgres -tAc "create database $DATABASE" >/dev/null
(cd "$PROJECT" && dotnet build -v q >/dev/null 2>&1) || { echo "FAIL  the C# project builds"; exit 1; }
(cd "$GO_WORKER" && go build -o "$WORK/go-worker" ./cmd/worker) || { echo "FAIL  the Go worker builds"; exit 1; }
"$LANG_SH" migrate "$PROJECT" "$NAME" >/dev/null || { echo "FAIL  the C# migrations apply"; exit 1; }

TOKEN="$("$LANG_SH" console "$PROJECT" "$NAME" dev-token '{"subject":"interop-user","email":"interop@example.com"}' 2>/dev/null)"
API_PID="$(MORTIS_LOG="$WORK/api.log" "$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done

check "C#: a product is created through the Api" "201" "$(call -X POST -d '{"sku":"IOP-1","name":"Bolt","stock":10}' "http://localhost:$PORT/products")"
check "C#: a customer is created through the Api" "201" "$(call -X POST -d '{"name":"Ada","email":"ada@example.com"}' "http://localhost:$PORT/customers")"
CUSTOMER="$(field id)"
check "C#: an order of 3 is created through the Api" "201" "$(call -X POST -d "{\"orderNumber\":\"IOP-1\",\"customerId\":\"$CUSTOMER\",\"lines\":[{\"sku\":\"IOP-1\",\"price\":1,\"currency\":\"EUR\",\"quantity\":3}]}" "http://localhost:$PORT/orders")"
ORDER="$(field id)"
sleep 2
check "C#: the Api only wrote the row (no worker in the Api), the stock is untouched" "1|10" "$(sql "select count(*) from outbox_messages")|$(sql "select stock from products where sku = 'IOP-1'")"
check "the row names the C# consumer and event type, the payload is the C# serialization" "Boilerplate.Application.Products.DecreaseStockWhenOrderLineAddedEventConsumer|Boilerplate.Domain.Aggregates.Orders.Events.LineAdded|IOP-1" "$(sql "select consumer || '|' || type || '|' || (payload->>'Sku') from outbox_messages")"

(cd "$GO_WORKER" && ConnectionStrings__Default="postgres://${PGUSER:-postgres}:${PGPASSWORD:-postgres}@${PGHOST:-localhost}:${PGPORT:-5432}/$DATABASE" exec "$WORK/go-worker" >"$WORK/go.log" 2>&1) &
GO_PID=$!
check "Go: the Go consumer moved the stock (10 - 3) and deleted the row" "7|0" "$(eventually "select (select stock from products where sku = 'IOP-1') || '|' || (select count(*) from outbox_messages)" "7|0")"
check "Go: the stock was changed as the user whose C# command added the line" "interop-user" "$(sql "select updated_user_id from products where sku = 'IOP-1'")"
LINE="$(sql "select id from order_lines where order_id = '$ORDER'")"
check "C#: cancelling the line through the Api answers 200" "200" "$(call -X PUT -d '{"reason":"interop"}' "http://localhost:$PORT/orders/$ORDER/lines/$LINE/cancel")"
check "Go: the Go consumer gave the units back (7 + 3)" "10|0" "$(eventually "select (select stock from products where sku = 'IOP-1') || '|' || (select count(*) from outbox_messages)" "10|0")"
check "Go: the worker logged each handled row as a JSON line" "2" "$(grep -c '"Handled Boilerplate.Domain.Aggregates.Orders.Events.Line' "$WORK/go.log")"

echo "$PASSED passed, $FAILED failed"
[ "$FAILED" = 0 ]
