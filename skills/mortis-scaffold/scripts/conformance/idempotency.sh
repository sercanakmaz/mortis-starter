#!/usr/bin/env bash
# Conformance: the idempotent-commands add-on. A repeated POST with the same key returns the first response and writes once.
# Needs: PROJECT NAME LANG_SH WORK DATABASE API_PORT, TOKEN with auth = jwt, a migrated PostgreSQL, psql, curl.
# Applies when: addons contains idempotent-commands, example = true, database = postgres; after example-http.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

S="$(date +%s%N)"
PORT=$((API_PORT + 10))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done
post() { api_curl -s -D "$WORK/idempotency.$1.headers" -o "$WORK/idempotency.$1.json" -w "%{http_code}" -H "Content-Type: application/json" -H "Idempotency-Key: $2" -X POST -d "$3" "http://localhost:$PORT/products"; }
header() { tr -d '\r' < "$WORK/idempotency.$1.headers" | awk -F': ' -v name="$2" 'tolower($1) == tolower(name) {print $2}'; }
PRODUCT="{\"sku\":\"IDM-$S\",\"name\":\"Once\",\"stock\":3}"

FIRST="$(post first "key-$S" "$PRODUCT")"
SECOND="$(post second "key-$S" "$PRODUCT")"
expect_equal "idempotency: the first POST creates the product" "201" "$FIRST"
expect_equal "idempotency: a repeat with the same key returns the first status, body and Location, marked replayed" "201|same|same|true" "$SECOND|$(cmp -s "$WORK/idempotency.first.json" "$WORK/idempotency.second.json" && echo same || echo different)|$([ "$(header first Location)" = "$(header second Location)" ] && echo same || echo different)|$(header second Idempotency-Replayed)"
expect_equal "idempotency: ... and one row exists" "1" "$(sql "select count(*) from products where sku = 'IDM-$S'")"
expect_equal "idempotency: the same key with another body is a 422 idempotency_key_reused" "422|idempotency_key_reused" "$(post other "key-$S" "{\"sku\":\"IDM2-$S\",\"name\":\"Other\",\"stock\":1}")|$(json_field "$(cat "$WORK/idempotency.other.json")" "['code']")"
DUP_STATUS="$([ "${EXCEPTIONS:-coded}" = "minimal" ] && echo 500 || echo 409)"
expect_equal "idempotency: a POST without the header runs as before (the duplicate sku is refused by the command itself)" "$DUP_STATUS" "$(api_curl -s -o /dev/null -w "%{http_code}" -H "Content-Type: application/json" -X POST -d "$PRODUCT" "http://localhost:$PORT/products")"
expect_equal "idempotency: a failed request frees its key, so a corrected retry with the same key runs" "422|201" "$(post bad "fix-$S" '{"sku":"","stock":-1}')|$(post fixed "fix-$S" "{\"sku\":\"IDM3-$S\",\"name\":\"Fixed\",\"stock\":1}")"

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
