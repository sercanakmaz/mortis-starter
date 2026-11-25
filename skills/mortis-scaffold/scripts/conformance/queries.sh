#!/usr/bin/env bash
# Conformance: the query side. With queries = commands, no query type exists. With split or read-models: paged, filtered, sorted
# lists and lookups over HTTP and the Console; with read-models, OrderSummary equals its order and is rebuilt from empty.
# The split and read-models checks are NOT RUN YET (decision 46): written in phase 1 from queries.md and read-models.md.
# Needs: PROJECT NAME LANG_SH WORK QUERIES; for split and read-models DATABASE API_PORT TOKEN (auth = jwt), a migrated PostgreSQL, psql, curl.
# Applies when: example = true, database = postgres; after example-http.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

QUERIES="${QUERIES:-commands}"

if [ "$QUERIES" = "commands" ]; then
  expect_equal "queries = commands: no query type is generated (IQuery, *Query.cs, readers)" "0" "$(find "$PROJECT" -name 'IQuery.cs' -o -name '*Query.cs' -o -name 'I*Reader.cs' -o -name '*_query.go' -o -name 'reader.go' | grep -v '/bin/\|/obj/' | wc -l | tr -d ' ')"
  report
  [ "$FAILED" -eq 0 ]
  exit
fi

S="$(date +%s%N)"
PORT=$((API_PORT + 7))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done
get() { api_curl -s -o "$WORK/query.json" -w "%{http_code}" "http://localhost:$PORT$1"; }
post() { api_curl -s -o "$WORK/query.json" -X POST -H "Content-Type: application/json" -d "$2" "http://localhost:$PORT$1" >/dev/null; }
field() { python3 -I -c "import json,sys; d=json.load(open(sys.argv[1])); print(d$1)" "$WORK/query.json" 2>/dev/null; }
value() { python3 -I -c "import json,sys; d=json.load(open(sys.argv[1])); print($1)" "$WORK/query.json" 2>/dev/null; }

T="QN$S"
for item in "QX-$S-1 Charlie 5" "QX-$S-2 Alpha 9" "QX-$S-3 Bravo 1"; do
  set -- $item
  post /products "{\"sku\":\"$1\",\"name\":\"$2 $T\",\"stock\":$3}"
done
post /products "{\"sku\":\"QX-$S-4\",\"name\":\"Other $S\",\"stock\":1}"
PRODUCT="$(field "['id']")"
names() { value "', '.join(i['name'] for i in d['items'])" | sed "s/ $T//g"; }

expect_equal "split: a filtered list sorted by name, first page of two, with the total" "200|Alpha, Bravo|3|1|2" "$(get "/products?page=1&size=2&sort=NameAscending&nameContains=$T")|$(names)|$(field "['total']")|$(field "['page']")|$(field "['size']")"
expect_equal "split: the second page holds the rest, without overlap" "Charlie" "$(get "/products?page=2&size=2&sort=NameAscending&nameContains=$T" >/dev/null; names)"
expect_equal "split: sort by stock, descending" "9, 5, 1" "$(get "/products?size=10&sort=StockDescending&nameContains=$T" >/dev/null; value "', '.join(str(i['stock']) for i in d['items'])")"
expect_equal "split: a page size out of range is a 422 validation_failed, before any read" "422|validation_failed" "$(get "/products?size=0")|$(field "['code']")"
expect_equal "split: a lookup by id answers the product, and an unknown id is a 404" "200|404" "$(get "/products/$PRODUCT")|$(get "/products/$(new_id)")"
expect_equal "split: the Console runs a query by name" "3" "$(console_run ListProducts "{\"nameContains\":\"$T\"}" 2>/dev/null | python3 -I -c "import json,sys; print(json.load(sys.stdin)['Total'])")"

if [ "$QUERIES" = "read-models" ]; then
  post /customers "{\"name\":\"Summary $S\",\"email\":\"summary-$S@example.com\"}"
  CUSTOMER="$(field "['id']")"
  post /orders "{\"orderNumber\":\"SUM-$S\",\"customerId\":$(jid "$CUSTOMER"),\"lines\":[{\"sku\":\"QX-$S-1\",\"price\":2,\"currency\":\"EUR\",\"quantity\":3},{\"sku\":\"QX-$S-2\",\"price\":5,\"currency\":\"EUR\",\"quantity\":1}]}"
  ORDER="$(field "['id']")"
  LINE="$(field "['lines'][1]['id']")"
  api_curl -s -o /dev/null -X PUT -H "Content-Type: application/json" -d '{"reason":"changed mind"}' "http://localhost:$PORT/orders/$ORDER/lines/$LINE/cancel"
  api_curl -s -o /dev/null -X PUT "http://localhost:$PORT/orders/$ORDER/complete"
  SUMMARY_DIFF="select count(*) from (select o.id, o.order_number, c.name, o.status, coalesce(sum(l.price_amount * l.quantity) filter (where not l.is_cancelled), 0) as total, count(l.id) filter (where not l.is_cancelled) as lines from orders o join customers c on c.id = o.customer_id left join order_lines l on l.order_id = o.id group by o.id, c.name except select order_id, order_number, customer_name, status, total_amount, line_count from order_summaries) as missing"
  expect_equal "read-models: the summary equals its order after add, cancel and complete (Completed, total 6, one line)" "SUM-$S|Summary $S|Completed|6.0000|1" "$(sql "select order_number || '|' || customer_name || '|' || status || '|' || total_amount || '|' || line_count from order_summaries where order_id = '$ORDER'")"
  expect_equal "read-models: every order of the example's flows has an equal summary" "0" "$(sql "$SUMMARY_DIFF")"
  expect_equal "read-models: the summaries list answers over HTTP" "200|SUM-$S" "$(get "/orders/summaries?size=100&status=Completed")|$(value "next(i['orderNumber'] for i in d['items'] if i['orderNumber'] == 'SUM-$S')")"
  sql "delete from order_summaries" >/dev/null
  console_run rebuild-order-summary >/dev/null 2>"$WORK/rebuild.err"
  expect_equal "read-models: rebuild-order-summary recreates every summary from an empty table" "0|0|$(sql "select count(*) from orders")" "$?|$(sql "$SUMMARY_DIFF")|$(sql "select count(*) from order_summaries")"
fi

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
