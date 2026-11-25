#!/usr/bin/env bash
# Conformance: permissions (auth = jwt). Every command declares a permission; the caller must hold it; reactions run as the user.
# NOT RUN YET (decision 47): written in phase 1 from permissions.md, first run in phase 2. Assumes PermissionsFormat = List.
# Needs: PROJECT NAME LANG_SH WORK DATABASE API_PORT TENANT_A (with tenancy), a migrated PostgreSQL, psql, curl; the worker running
# when hosts.worker = separate.
# Applies when: auth = jwt, example = true, database = postgres; after auth.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

S="$(date +%s%N)"
PORT=$((API_PORT + 8))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done
TENANT_CLAIM="${TENANT_A:+,\"tenant\":\"$TENANT_A\"}"
token() { dev_token "{\"subject\":\"$1\",\"email\":\"$1@example.com\",\"permissions\":[$2]$TENANT_CLAIM}"; }
call() { local bearer="$1"; shift; curl -s -o "$WORK/permissions.json" -w "%{http_code}" -H "Authorization: Bearer $bearer" -H "Content-Type: application/json" "$@"; }
field() { python3 -I -c "import json,sys; d=json.load(open(sys.argv[1])); print(d$1)" "$WORK/permissions.json" 2>/dev/null; }

READER="$(token "reader-$S" '"products.read"')"
WRITER="$(token "writer-$S" '"products.create","products.read"')"
expect_equal "permissions: a token without the command's permission is a 403 with the standard body, and nothing is written" "403|forbidden|0" "$(call "$READER" -X POST -d "{\"sku\":\"PRM-$S\",\"name\":\"Nope\",\"stock\":1}" "http://localhost:$PORT/products")|$(field "['code']")|$(sql "select count(*) from products where sku = 'PRM-$S'")"
expect_equal "permissions: the refusal comes before validation (an invalid command without the permission is still a 403)" "403|forbidden" "$(call "$READER" -X POST -d '{"sku":"","stock":-1}' "http://localhost:$PORT/products")|$(field "['code']")"
expect_equal "permissions: with the permission the write succeeds, audited as the token's user" "201|writer-$S" "$(call "$WRITER" -X POST -d "{\"sku\":\"PRM-$S\",\"name\":\"Yes\",\"stock\":10}" "http://localhost:$PORT/products")|$(sql "select created_user_id from products where sku = 'PRM-$S'")"

ORDERER="$(token "orderer-$S" '"customers.create","orders.create"')"
FULL="$(token "full-$S" '"customers.create","orders.create","products.decrease-stock"')"
call "$ORDERER" -X POST -d "{\"name\":\"Orderer\",\"email\":\"orderer-$S@example.com\"}" "http://localhost:$PORT/customers" >/dev/null
CUSTOMER="$(field "['id']")"
call "$ORDERER" -X POST -d "{\"orderNumber\":\"PRM-$S\",\"customerId\":$(jid "$CUSTOMER"),\"lines\":[{\"sku\":\"PRM-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":2}]}" "http://localhost:$PORT/orders" >/dev/null
sleep 5
expect_equal "permissions: a consumer's command runs as the order's user, so without products.decrease-stock the stock stays" "10" "$(sql "select stock from products where sku = 'PRM-$S'")"
call "$FULL" -X POST -d "{\"orderNumber\":\"PRM2-$S\",\"customerId\":$(jid "$CUSTOMER"),\"lines\":[{\"sku\":\"PRM-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":3}]}" "http://localhost:$PORT/orders" >/dev/null
expect_equal "permissions: ... and with it the consumer decreases the stock, audited as that user" "7|full-$S" "$(eventually "select stock || '|' || updated_user_id from products where sku = 'PRM-$S'" "7|full-$S" 20)"

console_run CreateProduct "{\"sku\":\"PRMC-$S\",\"name\":\"Console\",\"stock\":1}" >/dev/null 2>"$WORK/permissions.err"
expect_equal "permissions: a Console run without --permissions is refused" "2|forbidden" "$?|$(error_code "$WORK/permissions.err")"
console_run --permissions products.create CreateProduct "{\"sku\":\"PRMC-$S\",\"name\":\"Console\",\"stock\":1}" >/dev/null 2>&1
expect_equal "permissions: ... and with --permissions it runs" "1" "$(sql "select count(*) from products where sku = 'PRMC-$S'")"

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
