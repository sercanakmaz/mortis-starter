#!/usr/bin/env bash
# Conformance: the caching add-on. Reads are cached, and a read after the command that changes the data is fresh.
# NOT RUN YET (decision 50): written in phase 1 from caching.md, first run in phase 2.
# Needs: PROJECT NAME LANG_SH WORK DATABASE API_PORT WORKER, TOKEN with auth = jwt, a migrated PostgreSQL, psql, curl; the worker running when hosts.worker = separate.
# Applies when: addons contains caching, example = true, database = postgres; after example-http.sh. With hosts.worker = separate the freshness check needs ConnectionStrings__Redis (each process has its own memory cache otherwise).
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

S="$(date +%s%N)"
PORT=$((API_PORT + 14))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done
stock() { api_curl -s "http://localhost:$PORT/products/$1" | python3 -I -c 'import sys, json; print(json.load(sys.stdin)["stock"])'; }

PRODUCT="$(api_curl -s -H "Content-Type: application/json" -X POST -d "{\"sku\":\"CCH-$S\",\"name\":\"Cached\",\"stock\":10}" "http://localhost:$PORT/products" | python3 -I -c 'import sys, json; print(json.load(sys.stdin)["id"])')"
stock "$PRODUCT" >/dev/null
sql "update products set stock = 99 where id = '$PRODUCT'" >/dev/null
expect_equal "caching: a second read is served from the cache (a change made behind the application is not seen yet)" "10" "$(stock "$PRODUCT")"
sql "update products set stock = 10 where id = '$PRODUCT'" >/dev/null

if [ "${WORKER:-none}" = "separate" ] && [ -z "${ConnectionStrings__Redis:-}" ]; then
  echo "SKIP  caching: freshness across the API and the Worker needs ConnectionStrings__Redis"
else
  CUSTOMER="$(api_curl -s -H "Content-Type: application/json" -X POST -d "{\"name\":\"Cch\",\"email\":\"cch-$S@example.com\"}" "http://localhost:$PORT/customers" | python3 -I -c 'import sys, json; print(json.load(sys.stdin)["id"])')"
  api_curl -s -o /dev/null -H "Content-Type: application/json" -X POST -d "{\"orderNumber\":\"CCH-$S\",\"customerId\":\"$CUSTOMER\",\"lines\":[{\"sku\":\"CCH-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":4}]}" "http://localhost:$PORT/orders"
  eventually "select stock from products where id = '$PRODUCT'" "6" 20 >/dev/null
  expect_equal "caching: once the command that decreased the stock has committed, the next read is fresh" "6" "$(stock "$PRODUCT")"
fi

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
