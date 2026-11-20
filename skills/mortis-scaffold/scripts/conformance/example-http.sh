#!/usr/bin/env bash
# Conformance: the example domain's HTTP requests: status codes and error bodies
# Needs: PROJECT NAME LANG_SH WORK DATABASE (and API_PORT for http.sh), a reachable PostgreSQL, psql, curl.
# Applies when: example = true; EXCEPTIONS selects the expected status of database failures, database = postgres; after example-console.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

# The failure table of failures.md: database failures are translated from the coded level up.
if [ "${EXCEPTIONS:-coded}" = "minimal" ]; then
  DUP_STATUS=500; DUP_CODE=unexpected_error; REF_STATUS=500; REF_CODE=unexpected_error
else
  DUP_STATUS=409; DUP_CODE=duplicate; REF_STATUS=400; REF_CODE=reference_violation
fi

# status codes and error bodies
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$API_PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$API_PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done

request() { curl -s -o "$WORK/body.json" -w "%{http_code}" -X "$1" "http://localhost:$API_PORT$2" -H "Content-Type: application/json" ${3:+-d "$3"}; }
body_field() { python3 -c "import json,sys; d=json.load(open('$WORK/body.json')); print(d$1)"; }

expect_equal "api: POST /categories creates a category" "201" "$(request POST /categories '{"name":"Tools"}')"
CATEGORY_ID="$(body_field "['id']")"
expect_equal "api: ... with the integer id the database assigned" "True" "$(python3 -I -c "import sys; print(sys.argv[1].isdigit() and int(sys.argv[1]) > 0)" "$CATEGORY_ID")"
expect_equal "api: GET /categories/{id} reads it back by that id" "200|Tools" "$(request GET "/categories/$CATEGORY_ID")|$(body_field "['name']")"
expect_equal "api: a product may name the category" "201|$CATEGORY_ID" "$(request POST /products "{\"sku\":\"CAT-1\",\"name\":\"In a category\",\"stock\":1,\"categoryId\":$CATEGORY_ID}")|$(body_field "['categoryId']")"
expect_equal "api: a product naming an unknown category is refused: $REF_STATUS $REF_CODE" "$REF_STATUS|$REF_CODE" "$(request POST /products '{"sku":"CAT-2","name":"Lost","stock":1,"categoryId":999999}')|$(body_field "['code']")"
expect_equal "api: POST /products creates a product" "201" "$(request POST /products '{"sku":"S","name":"Small","stock":100}')"
expect_equal "api: the same sku again is a $DUP_STATUS" "$DUP_STATUS" "$(request POST /products '{"sku":"S","name":"Small","stock":100}')"
expect_equal "api: POST /customers creates a customer" "201" "$(request POST /customers '{"name":"Ada","email":"Ada@Example.com"}')"
CUSTOMER_ID="$(body_field "['id']")"
expect_equal "api: the email was normalized by the aggregate" "ada@example.com" "$(body_field "['email']")"
expect_equal "api: the same email again is a $DUP_STATUS" "$DUP_STATUS" "$(request POST /customers '{"name":"Ada","email":"ada@example.com"}')"
expect_equal "api: ... with the code $DUP_CODE" "$DUP_CODE" "$(body_field "['code']")"
expect_equal "api: an order without lines is a 422" "422" "$(request POST /orders "{\"orderNumber\":\"API-1\",\"customerId\":\"$CUSTOMER_ID\",\"lines\":[]}")"
expect_equal "api: ... that names the failing field" "Lines" "$(body_field "['errors'].__iter__().__next__()")"
expect_equal "api: an order for a missing customer is refused by the foreign key: $REF_STATUS" "$REF_STATUS" "$(request POST /orders '{"orderNumber":"API-2","customerId":"01900000-0000-7000-8000-000000000000","lines":[{"sku":"S","price":1,"currency":"EUR","quantity":1}]}')"
expect_equal "api: ... with the code $REF_CODE" "$REF_CODE" "$(body_field "['code']")"
expect_equal "api: ... and no order row was left behind" "0" "$(sql "select count(*) from orders where order_number = 'API-2'")"
expect_equal "api: an order line with an unknown sku is refused by the foreign key: $REF_STATUS" "$REF_STATUS" "$(request POST /orders "{\"orderNumber\":\"API-4\",\"customerId\":\"$CUSTOMER_ID\",\"lines\":[{\"sku\":\"NO-SUCH-SKU\",\"price\":1,\"currency\":\"EUR\",\"quantity\":1}]}")"
expect_equal "api: ... with the code $REF_CODE" "$REF_CODE" "$(body_field "['code']")"
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
expect_equal "api: and the async handler gave the unit back (100 - 2 - 1 + 1)" "98" "$(eventually "select stock from products where sku = 'S'" "98" 10)"

request POST /products '{"sku":"HOT","name":"Hot item","stock":8}' >/dev/null
CONCURRENT_PIDS=()
for index in $(seq 1 8); do
  curl -s -o /dev/null -X POST "http://localhost:$API_PORT/orders" -H "Content-Type: application/json" \
    -d "{\"orderNumber\":\"CON-$index\",\"customerId\":\"$CUSTOMER_ID\",\"lines\":[{\"sku\":\"HOT\",\"price\":1,\"currency\":\"EUR\",\"quantity\":1}]}" &
  CONCURRENT_PIDS+=($!)
done
wait "${CONCURRENT_PIDS[@]}"
expect_equal "api: 8 concurrent orders of one product were all accepted" "8" "$(sql "select count(*) from orders where order_number like 'CON-%'")"
expect_equal "api: no stock decrement was lost to a race (8 - 8; with an outbox, retried until done)" "0" "$(eventually "select stock from products where sku = 'HOT'" "0" 40)"

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null

report
[ "$FAILED" -eq 0 ]
