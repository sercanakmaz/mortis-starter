#!/usr/bin/env bash
# Conformance: tenancy.mode = interface. Rows of a tenant are invisible to another; global rows are visible to all; consumers keep the tenant.
# Needs: PROJECT NAME LANG_SH WORK DATABASE API_PORT TENANT_A TENANT_B, a migrated PostgreSQL, psql, curl; the worker running when hosts.worker = separate.
# Applies when: tenancy.mode = interface, auth = jwt, example = true, database = postgres; after example-http.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

S="$(date +%s%N)"
PORT=$((API_PORT + 5))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done

A="$(dev_token "{\"subject\":\"user-a\",\"email\":\"a@example.com\",\"tenant\":\"$TENANT_A\"}")"
B="$(dev_token "{\"subject\":\"user-b\",\"email\":\"b@example.com\",\"tenant\":\"$TENANT_B\"}")"
NONE="$(dev_token '{"subject":"user-none","email":"none@example.com"}')"
as() { local token="$1"; shift; curl -s -o "$WORK/tenancy.json" -w "%{http_code}" -H "Authorization: Bearer $token" -H "Content-Type: application/json" "$@"; }
field() { python3 -I -c "import json,sys; d=json.load(open(sys.argv[1])); print(d$1)" "$WORK/tenancy.json" 2>/dev/null; }
url() { printf 'http://localhost:%s%s' "$PORT" "$1"; }

as "$A" -X POST -d "{\"sku\":\"T-$S\",\"name\":\"A's\",\"stock\":10}" "$(url /products)" >/dev/null
PRODUCT_A="$(field "['id']")"
expect_equal "tenancy: the same sku can exist in two tenants" "201" "$(as "$B" -X POST -d "{\"sku\":\"T-$S\",\"name\":\"B's\",\"stock\":10}" "$(url /products)")"
expect_equal "tenancy: the same sku twice in one tenant is a 409 duplicate" "409|duplicate" "$(as "$A" -X POST -d "{\"sku\":\"T-$S\",\"name\":\"again\",\"stock\":1}" "$(url /products)")|$(field "['code']")"
expect_equal "tenancy: tenant A reads its own product" "200" "$(as "$A" "$(url "/products/$PRODUCT_A")")"
expect_equal "tenancy: tenant B cannot read tenant A's product" "404" "$(as "$B" "$(url "/products/$PRODUCT_A")")"
as "$A" -X POST -d "{\"name\":\"A customer\",\"email\":\"t-$S@example.com\"}" "$(url /customers)" >/dev/null
CUSTOMER_A="$(field "['id']")"
expect_equal "tenancy: tenant B cannot change tenant A's customer" "404|false" "$(as "$B" -X PUT "$(url "/customers/$CUSTOMER_A/block")")|$(sql "select is_blocked from customers where id = '$CUSTOMER_A'" | sed 's/^f$/false/; s/^t$/true/')"
expect_equal "tenancy: a caller without a tenant cannot write tenant-owned data: 403 tenant_required" "403|tenant_required|0" "$(as "$NONE" -X POST -d "{\"sku\":\"N-$S\",\"name\":\"nobody's\",\"stock\":1}" "$(url /products)")|$(field "['code']")|$(sql "select count(*) from products where sku = 'N-$S'")"
expect_equal "tenancy: a caller without a tenant cannot read tenant-owned data: 403" "403" "$(as "$NONE" "$(url "/products/$PRODUCT_A")")"
CONSOLE_TENANT="" console_run CreateProduct "{\"sku\":\"NC-$S\",\"name\":\"console\",\"stock\":1}" >/dev/null 2>"$WORK/tenancy.err"
expect_equal "tenancy: a Console run without --tenant cannot write tenant-owned data" "2|tenant_required" "$?|$(error_code "$WORK/tenancy.err")"

as "$A" -X POST -d "{\"name\":\"Global $S\"}" "$(url /categories)" >/dev/null
CATEGORY="$(field "['id']")"
expect_equal "tenancy: a global category is readable from another tenant and from none" "200|200" "$(as "$B" "$(url "/categories/$CATEGORY")")|$(as "$NONE" "$(url "/categories/$CATEGORY")")"

as "$B" -X POST -d "{\"sku\":\"ONLY-B-$S\",\"name\":\"B only\",\"stock\":5}" "$(url /products)" >/dev/null
expect_equal "tenancy: an order line cannot reference another tenant's product: 400 reference_violation" "400|reference_violation" "$(as "$A" -X POST -d "{\"orderNumber\":\"TX-$S\",\"customerId\":$(jid "$CUSTOMER_A"),\"lines\":[{\"sku\":\"ONLY-B-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":1}]}" "$(url /orders)")|$(field "['code']")"

as "$B" -X POST -d "{\"name\":\"B customer\",\"email\":\"t-$S@example.com\"}" "$(url /customers)" >/dev/null
CUSTOMER_B="$(field "['id']")"
for order in "A 1" "B 4" "A 2"; do
  set -- $order
  token="$A"; customer="$CUSTOMER_A"; [ "$1" = "B" ] && { token="$B"; customer="$CUSTOMER_B"; }
  as "$token" -X POST -d "{\"orderNumber\":\"TI-$1-$2-$S\",\"customerId\":$(jid "$customer"),\"lines\":[{\"sku\":\"T-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":$2}]}" "$(url /orders)" >/dev/null
done
expect_equal "tenancy: with both tenants' events interleaved, each consumer moved only its own tenant's stock (A 10-1-2, B 10-4)" "7|6" "$(eventually "select string_agg(stock::text, '|' order by tenant_id = '$TENANT_B') from products where sku = 'T-$S'" "7|6" 20)"

if [ "${TENANCY_KEY:-guid}" = "text" ]; then
  SPACED="$(dev_token "{\"subject\":\"user-s\",\"email\":\"s@example.com\",\"tenant\":\"acme corp\"}")"
  expect_equal "tenancy (key text): a tenant id with a space is refused: 403" "403" "$(as "$SPACED" "$(url "/products/$PRODUCT_A")")"
  expect_equal "tenancy (key text): tenant_id is text, at most 64 characters, on every tenant-owned table" "0" "$(sql "select count(*) from information_schema.columns where table_schema = 'public' and column_name = 'tenant_id' and table_name <> 'outbox_messages' and (data_type <> 'character varying' or character_maximum_length <> 64)")"
else
  expect_equal "tenancy (key guid): tenant_id is a uuid on every tenant-owned table" "0" "$(sql "select count(*) from information_schema.columns where table_schema = 'public' and column_name = 'tenant_id' and table_name <> 'outbox_messages' and data_type <> 'uuid'")"
fi
expect_equal "tenancy: every unique index on a tenant-owned table starts with tenant_id" "0" "$(sql "select count(*) from pg_index i join pg_class t on t.oid = i.indrelid join pg_attribute a on a.attrelid = t.oid and a.attnum = i.indkey[0] where i.indisunique and not i.indisprimary and t.relname in ('products', 'customers', 'orders', 'order_lines') and a.attname <> 'tenant_id'")"

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
