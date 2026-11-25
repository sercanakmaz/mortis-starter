#!/usr/bin/env bash
# Phase 3 step 03, the bus: the C# advanced build (tenancy, background outbox, RabbitMQ, a separate Worker) and the Go bus of
# reference/interop/go-worker exchange the example's LineAdded event through RabbitMQ, with the context intact:
#   C# to Go: an order created with the C# Console is relayed by the C# Worker's outbox to RabbitMQ; a Go endpoint receives it with its tenant, user and correlation.
#   Go to C#: an event the Go publisher sends moves the stock through the C# Worker's consumer, in the right tenant, as the Go user.
# The MassTransit 8 side (wire-format checks 1 to 5 for Go) is tests/check_interop.sh. Needs PostgreSQL (PG*), RabbitMQ at
# localhost:5672 (guest/guest), .NET and Go. Usage: bash tests/check_interop_bus.sh
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/reference/variants/advanced"
LANG_SH="$ROOT/skills/mortis-scaffold/scripts/lang/csharp.sh"
NAME=Boilerplate
DATABASE=mortis_interop_bus
WORK="$(mktemp -d)"
TENANT_ID="$(python3 -I -c 'import uuid; print(uuid.uuid4())')"
export ConnectionStrings__Default="Host=${PGHOST:-localhost};Port=${PGPORT:-5432};Database=$DATABASE;Username=${PGUSER:-postgres};Password=${PGPASSWORD:-postgres}"
export ConnectionStrings__RabbitMq="amqp://guest:guest@localhost:5672/" DOTNET_ENVIRONMENT=Development
export MORTIS_LINE_ADDED_WIRE_NAME="Boilerplate.Contracts.Orders:LineAdded"
PASSED=0
FAILED=0
WORKER_PID=""
GO_PID=""

stop() { for pid in $WORKER_PID $GO_PID; do kill "$pid" 2>/dev/null; wait "$pid" 2>/dev/null; done; }
trap stop EXIT
check() { if [ "$2" = "$3" ]; then echo "PASS  $1"; PASSED=$((PASSED + 1)); else echo "FAIL  $1"; echo "      expected '$2', got '$3'"; FAILED=$((FAILED + 1)); fi; }
sql() { psql -d "$DATABASE" -tAc "$1"; }
eventually() { local query="$1" expected="$2" value=""; for _ in $(seq 1 90); do value="$(sql "$query")"; [ "$value" = "$expected" ] && break; sleep 0.5; done; printf '%s' "$value"; }
console() { "$LANG_SH" console "$PROJECT" "$NAME" --user cs-user --email cs@example.com --tenant "$TENANT_ID" "$@" 2>>"$WORK/console.log"; }
field() { python3 -I -c "import json,sys; d=json.loads(sys.stdin.read()); print(next(v for k, v in d.items() if k.lower() == sys.argv[1].lower()))" "$1"; }

psql -d postgres -tAc "drop database if exists $DATABASE with (force)" >/dev/null
psql -d postgres -tAc "create database $DATABASE" >/dev/null
(cd "$PROJECT" && dotnet build -v q >/dev/null 2>&1) || { echo "FAIL  the C# project builds"; exit 1; }
(cd "$ROOT/reference/interop/go-worker" && go build -o "$WORK/gobus" ./cmd/gobus) || { echo "FAIL  the Go bus builds"; exit 1; }
"$LANG_SH" migrate "$PROJECT" "$NAME" >/dev/null || { echo "FAIL  the C# migrations apply"; exit 1; }
"$WORK/gobus" declare --queue interop-bus-go >/dev/null

WORKER_PID="$(MORTIS_LOG="$WORK/worker.log" "$LANG_SH" worker-start "$PROJECT" "$NAME")"
sleep 6
"$WORK/gobus" consume --queue interop-bus-go --sku IOB-1 --out "$WORK/received.json" --timeout 90s >/dev/null 2>"$WORK/go.log" &
GO_PID=$!
sleep 2

console CreateProduct '{"sku":"IOB-1","name":"Bolt","stock":10}' >/dev/null
check "C#: a tenant-owned product is created" "10" "$(sql "select stock from products where sku = 'IOB-1' and tenant_id = '$TENANT_ID'")"
CUSTOMER="$(console CreateCustomer '{"name":"Ada","email":"ada@example.com"}' | field id)"
console CreateOrder "{\"orderNumber\":\"IOB-1\",\"customerId\":\"$CUSTOMER\",\"lines\":[{\"sku\":\"IOB-1\",\"price\":1,\"currency\":\"EUR\",\"quantity\":3}]}" >/dev/null
check "C#: the C# Worker's consumer took the 3 units (the C# path works)" "7" "$(eventually "select stock from products where sku = 'IOB-1'" "7")"
for _ in $(seq 1 60); do [ -s "$WORK/received.json" ] && break; sleep 0.5; done
read_go() { python3 -I -c "import json,sys; print(json.load(open(sys.argv[1]))[sys.argv[2]])" "$WORK/received.json" "$1" 2>/dev/null; }
check "C# to Go: the Go endpoint received the LineAdded the C# outbox relayed" "IOB-1|3" "$(read_go SKU)|$(read_go Quantity)"
check "C# to Go: with the tenant, the user and the email of the C# command" "$TENANT_ID|cs-user|cs@example.com" "$(read_go TenantID)|$(read_go UserID)|$(read_go UserEmail)"
check "C# to Go: with the correlation id the C# command logged" "yes" "$(grep -q "$(read_go CorrelationID)" "$WORK/console.log" && echo yes || echo no)"

EVENT_ID="$("$WORK/gobus" publish --sku IOB-1 --quantity 2 --tenant "$TENANT_ID" --user go-user --email go@example.com)"
check "Go to C#: the C# Worker's consumer took the 2 units the Go event names" "5" "$(eventually "select stock from products where sku = 'IOB-1'" "5")"
check "Go to C#: the stock was changed as the Go user, in the Go event's tenant" "go-user|$TENANT_ID" "$(sql "select updated_user_id || '|' || tenant_id from products where sku = 'IOB-1'")"
check "Go to C#: the C# Worker logged the Go event by its id" "yes" "$(grep -q "$EVENT_ID" "$WORK/worker.log" && echo yes || echo no)"
"$WORK/gobus" publish --sku IOB-1 --quantity 1 --tenant "$(python3 -I -c 'import uuid; print(uuid.uuid4())')" --user go-user >/dev/null
sleep 4
check "Go to C#: an event of another tenant does not reach this tenant's product" "5" "$(sql "select stock from products where sku = 'IOB-1'")"

echo "$PASSED passed, $FAILED failed"
[ "$FAILED" = 0 ]
