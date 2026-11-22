#!/usr/bin/env bash
# Conformance: the loop-guard add-on. Every command carries the chain that led to it; the example's reaction shows a chain of two.
# NOT RUN YET (decision 50): written in phase 1 from loop-guard.md, first run in phase 2.
# Needs: PROJECT NAME LANG_SH WORK API_PORT WORKER, TOKEN with auth = jwt, a migrated PostgreSQL, curl; the worker running when hosts.worker = separate.
# Applies when: addons contains loop-guard, example = true, database = postgres; after example-http.sh. The limit itself is proved by LoopGuardTests.
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

S="$(date +%s%N)"
PORT=$((API_PORT + 19))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done
api_curl -s -o /dev/null -H "Content-Type: application/json" -X POST -d "{\"sku\":\"LPG-$S\",\"name\":\"Looped\",\"stock\":5}" "http://localhost:$PORT/products"
CUSTOMER="$(api_curl -s -H "Content-Type: application/json" -X POST -d "{\"name\":\"Lpg\",\"email\":\"lpg-$S@example.com\"}" "http://localhost:$PORT/customers" | python3 -I -c 'import sys, json; print(json.load(sys.stdin)["id"])')"
api_curl -s -o /dev/null -H "Content-Type: application/json" -X POST -d "{\"orderNumber\":\"LPG-$S\",\"customerId\":\"$CUSTOMER\",\"lines\":[{\"sku\":\"LPG-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":1}]}" "http://localhost:$PORT/orders"
eventually "select stock from products where sku = 'LPG-$S'" "4" 20 >/dev/null
LOG="$WORK/loop-guard.log"
cat "$PROJECT/.mortis/api.log" > "$LOG"
[ "${WORKER:-none}" = "separate" ] && [ -f "$PROJECT/.mortis/worker.log" ] && cat "$PROJECT/.mortis/worker.log" >> "$LOG"
expect_equal "loop-guard: a request's command logs a chain of itself" "yes" "$([ "$(log_count "$LOG" "l.get('Message') == 'CreateOrderCommand started' and l.get('Chain') == 'CreateOrderCommand'")" -ge 1 ] && echo yes || echo no)"
expect_equal "loop-guard: the consumer's command logs the chain that led to it" "yes" "$([ "$(log_count "$LOG" "l.get('Message') == 'DecreaseProductStockCommand started' and l.get('Chain') == 'CreateOrderCommand -> DecreaseProductStockCommand'")" -ge 1 ] && echo yes || echo no)"

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
