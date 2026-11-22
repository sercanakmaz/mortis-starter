#!/usr/bin/env bash
# Conformance: auth = jwt. Bearer tokens decide who the caller is; the development token command works only in Development.
# Needs: PROJECT NAME LANG_SH WORK DATABASE API_PORT TOKEN TENANT_A (with tenancy), a migrated PostgreSQL, psql, curl.
# Applies when: auth = jwt, example = true, database = postgres; after example-console.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

S="$(date +%s%N)"
PORT=$((API_PORT + 4))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done

status() { curl -s -o "$WORK/auth.json" -w "%{http_code}" "$@"; }
TENANT_CLAIM="${TENANT_A:+,\"tenant\":\"$TENANT_A\"}"
USER_TOKEN="$(dev_token "{\"subject\":\"auth-$S\",\"email\":\"auth-$S@example.com\"$TENANT_CLAIM}")"
FORGED="$(python3 -I - "$USER_TOKEN" <<'PY'
import base64, hashlib, hmac, sys
header, payload, _ = sys.argv[1].split(".")
signature = hmac.new(b"not-the-development-key-0123456789-abcdefgh", f"{header}.{payload}".encode(), hashlib.sha256).digest()
print(f"{header}.{payload}." + base64.urlsafe_b64encode(signature).rstrip(b"=").decode())
PY
)"

expect_equal "auth: /health needs no token" "200" "$(status "http://localhost:$PORT/health")"
expect_equal "auth: a request without a token is refused with 401" "401" "$(status "http://localhost:$PORT/categories/1")"
expect_equal "auth: a token signed with another key is refused with 401" "401" "$(status -H "Authorization: Bearer $FORGED" "http://localhost:$PORT/categories/1")"
expect_equal "auth: with a valid token the write succeeds" "201" "$(status -X POST -H "Authorization: Bearer $USER_TOKEN" -H "Content-Type: application/json" -d "{\"sku\":\"AUTH-$S\",\"name\":\"Signed in\",\"stock\":1}" "http://localhost:$PORT/products")"
expect_equal "auth: ... and the audit columns hold the token's subject and email" "auth-$S|auth-$S@example.com" "$(sql "select created_user_id || '|' || created_user_name from products where sku = 'AUTH-$S'")"
DOTNET_ENVIRONMENT=Production "$LANG_SH" console "$PROJECT" "$NAME" dev-token '{"subject":"x"}' >"$WORK/prod.out" 2>"$WORK/prod.err"
expect_equal "auth: the development token command refuses to run outside Development" "2|forbidden|0" "$?|$(error_code "$WORK/prod.err")|$(wc -c <"$WORK/prod.out" | tr -d ' ')"

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
