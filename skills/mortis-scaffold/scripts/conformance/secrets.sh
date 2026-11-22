#!/usr/bin/env bash
# Conformance: the secrets add-on. A stored secret is unreadable in the database and readable through the port.
# NOT RUN YET (decision 50): written in phase 1 from secrets.md, first run in phase 2.
# Needs: PROJECT NAME LANG_SH WORK DATABASE API_PORT, TOKEN with auth = jwt, a migrated PostgreSQL, psql, curl; Secrets settings (the development key).
# Applies when: addons contains secrets, example = true, database = postgres; after example-http.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

S="$(date +%s%N)"
PORT=$((API_PORT + 18))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done
SECRET="TR-SECRET-$S"
CUSTOMER="$(api_curl -s -H "Content-Type: application/json" -X POST -d "{\"name\":\"Sec\",\"email\":\"sec-$S@example.com\"}" "http://localhost:$PORT/customers" | python3 -I -c 'import sys, json; print(json.load(sys.stdin)["id"])')"

expect_equal "secrets: setting the tax number answers 200" "200" "$(api_curl -s -o /dev/null -w "%{http_code}" -H "Content-Type: application/json" -X PUT -d "{\"taxNumber\":\"$SECRET\"}" "http://localhost:$PORT/customers/$CUSTOMER/tax-number")"
expect_equal "secrets: the column holds a v1 envelope, never the plaintext" "t|0" "$(sql "select tax_number like 'v1.%' from customers where id = '$CUSTOMER'")|$(sql "select count(*) from customers where tax_number like '%$SECRET%'")"
expect_equal "secrets: two customers with the same tax number get different envelopes" "2" "$(CUSTOMER2="$(api_curl -s -H "Content-Type: application/json" -X POST -d "{\"name\":\"Sec2\",\"email\":\"sec2-$S@example.com\"}" "http://localhost:$PORT/customers" | python3 -I -c 'import sys, json; print(json.load(sys.stdin)["id"])')"; api_curl -s -o /dev/null -H "Content-Type: application/json" -X PUT -d "{\"taxNumber\":\"$SECRET\"}" "http://localhost:$PORT/customers/$CUSTOMER2/tax-number"; sql "select count(distinct tax_number) from customers where id in ('$CUSTOMER', '$CUSTOMER2')")"
expect_equal "secrets: the API reads it back in plaintext through the port" "$SECRET" "$(api_curl -s "http://localhost:$PORT/customers/$CUSTOMER" | python3 -I -c 'import sys, json; print(json.load(sys.stdin)["taxNumber"])')"
expect_equal "secrets: no log line holds the plaintext" "0" "$(log_count "$PROJECT/.mortis/api.log" "'$SECRET' in str(l)")"

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
