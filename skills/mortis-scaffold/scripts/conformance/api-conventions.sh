#!/usr/bin/env bash
# Conformance: the api-conventions add-on. One error body for every failure, unknown members refused (opt-out per command), PATCH with Optional.
# NOT RUN YET (decision 48): written in phase 1 from api-conventions.md, first run in phase 2.
# Needs: PROJECT NAME LANG_SH WORK DATABASE API_PORT, TOKEN with auth = jwt, a migrated PostgreSQL, psql, curl.
# Applies when: addons contains api-conventions, example = true, database = postgres; after example-http.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

S="$(date +%s%N)"
PORT=$((API_PORT + 9))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done
BODY="$WORK/api-conventions.json"
call() { api_curl -s -o "$BODY" -w "%{http_code}" -H "Content-Type: application/json" "$@"; }
field() { python3 -I -c "import json,sys; d=json.load(open(sys.argv[1])); print(d$1)" "$BODY" 2>/dev/null; }
mentions() { grep -q "$1" "$BODY" && echo yes || echo no; }

expect_equal "api-conventions: a body with an unknown member is a 400 invalid_request naming it, and nothing is written" "400|invalid_request|yes|0" "$(call -X POST -d "{\"sku\":\"APC-$S\",\"name\":\"Typo\",\"stokc\":1}" "http://localhost:$PORT/products")|$(field "['code']")|$(mentions stokc)|$(sql "select count(*) from products where sku = 'APC-$S'")"
expect_equal "api-conventions: ... the same unknown member to the opted-out command (create customer) is accepted" "201" "$(call -X POST -d "{\"name\":\"Opt Out\",\"email\":\"apc-$S@example.com\",\"newsletter\":true}" "http://localhost:$PORT/customers")"
CUSTOMER="$(field "['id']")"
expect_equal "api-conventions: a body that is not JSON is a 400 invalid_request in the error body" "400|invalid_request" "$(call -X POST -d '{"sku":' "http://localhost:$PORT/products")|$(field "['code']")"
expect_equal "api-conventions: an unknown route is a 404 route_not_found in the error body" "404|route_not_found" "$(call "http://localhost:$PORT/no-such-route-$S")|$(field "['code']")"
expect_equal "api-conventions: an unknown method on a known route is a 405 method_not_allowed in the error body" "405|method_not_allowed" "$(call -X DELETE "http://localhost:$PORT/products")|$(field "['code']")"

call -X PATCH -d '{"note":"call before noon"}' "http://localhost:$PORT/customers/$CUSTOMER" >/dev/null
expect_equal "api-conventions: PATCH with {} changes nothing" "200|Opt Out|call before noon" "$(call -X PATCH -d '{}' "http://localhost:$PORT/customers/$CUSTOMER")|$(sql "select name || '|' || coalesce(note, '<null>') from customers where id = '$CUSTOMER'")"
expect_equal "api-conventions: PATCH with a null note clears the note and keeps the name" "200|Opt Out|<null>" "$(call -X PATCH -d '{"note":null}' "http://localhost:$PORT/customers/$CUSTOMER")|$(sql "select name || '|' || coalesce(note, '<null>') from customers where id = '$CUSTOMER'")"
expect_equal "api-conventions: PATCH with a null name is a validation failure on name (a required member cannot be cleared)" "400|yes" "$(call -X PATCH -d '{"name":null}' "http://localhost:$PORT/customers/$CUSTOMER")|$(python3 -I -c "import json,sys; d=json.load(open(sys.argv[1])); print('yes' if any(k.lower() == 'name' for k in (d.get('errors') or {})) else 'no')" "$BODY")"
expect_equal "api-conventions: the OpenAPI document is served" "200" "$(call "http://localhost:$PORT/openapi/v1.json")"

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
