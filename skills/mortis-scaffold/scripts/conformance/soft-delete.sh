#!/usr/bin/env bash
# Conformance: the soft-delete add-on. A deleted product is invisible, its SKU can be reused, and it can be restored.
# NOT RUN YET (decision 50): written in phase 1 from soft-delete.md, first run in phase 2.
# Needs: PROJECT NAME LANG_SH WORK DATABASE API_PORT EXCEPTIONS, TOKEN with auth = jwt, a migrated PostgreSQL, psql, curl.
# Applies when: addons contains soft-delete, example = true, database = postgres; after example-http.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

S="$(date +%s%N)"
PORT=$((API_PORT + 13))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done
call() { api_curl -s -o "$WORK/soft-delete.json" -w "%{http_code}" -H "Content-Type: application/json" "$@"; }
id() { json_field "$(cat "$WORK/soft-delete.json")" "['id']"; }
DUP_STATUS="$([ "${EXCEPTIONS:-coded}" = "minimal" ] && echo 500 || echo 409)"

call -X POST -d "{\"sku\":\"SDL-$S\",\"name\":\"Gone\",\"stock\":1}" "http://localhost:$PORT/products" >/dev/null
FIRST="$(id)"
expect_equal "soft-delete: DELETE answers 204 and keeps the row, marked deleted" "204|t" "$(call -X DELETE "http://localhost:$PORT/products/$FIRST")|$(sql "select is_deleted from products where id = '$FIRST'")"
expect_equal "soft-delete: a deleted product is invisible (404 not_found)" "404|not_found" "$(call "http://localhost:$PORT/products/$FIRST")|$(json_field "$(cat "$WORK/soft-delete.json")" "['code']")"
expect_equal "soft-delete: its SKU can be reused by a new product" "201" "$(call -X POST -d "{\"sku\":\"SDL-$S\",\"name\":\"Again\",\"stock\":1}" "http://localhost:$PORT/products")"
expect_equal "soft-delete: restoring the first while its SKU is taken is a duplicate" "$DUP_STATUS" "$(call -X POST "http://localhost:$PORT/products/$FIRST/restore")"
call -X POST -d "{\"sku\":\"SDL2-$S\",\"name\":\"Back\",\"stock\":1}" "http://localhost:$PORT/products" >/dev/null
SECOND="$(id)"
call -X DELETE "http://localhost:$PORT/products/$SECOND" >/dev/null
expect_equal "soft-delete: a deleted product can be restored and is visible again" "200|200|f" "$(call -X POST "http://localhost:$PORT/products/$SECOND/restore")|$(call "http://localhost:$PORT/products/$SECOND")|$(sql "select is_deleted from products where id = '$SECOND'")"

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
