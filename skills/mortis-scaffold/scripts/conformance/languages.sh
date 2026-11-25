#!/usr/bin/env bash
# Conformance: languages.mode = messages or data. Failures answer in the caller's language; translated data is stored per language.
# Needs: PROJECT NAME LANG_SH WORK DATABASE API_PORT TOKEN (auth = jwt), TENANT_A (with tenancy), a migrated PostgreSQL, psql, curl;
# the worker running when hosts.worker = separate.
# Applies when: languages.mode is not none, example = true, database = postgres; after example-http.sh
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

S="$(date +%s%N)"
PORT=$((API_PORT + 6))
API_PID="$("$LANG_SH" api-start "$PROJECT" "$NAME" "$PORT")"
for _ in $(seq 1 60); do curl -fs "http://localhost:$PORT/health" >/dev/null 2>&1 && break; sleep 0.5; done

TENANT_CLAIM="${TENANT_A:+,\"tenant\":\"$TENANT_A\"}"
TR="$(dev_token "{\"subject\":\"lang-tr\",\"email\":\"tr@example.com\",\"language\":\"tr\"$TENANT_CLAIM}")"
EN="$(dev_token "{\"subject\":\"lang-en\",\"email\":\"en@example.com\",\"language\":\"en\"$TENANT_CLAIM}")"
PLAIN="$(dev_token "{\"subject\":\"lang-none\",\"email\":\"none@example.com\"$TENANT_CLAIM}")"
call() { local token="$1" language="$2"; shift 2; curl -s -o "$WORK/lang.json" -H "Authorization: Bearer $token" -H "Content-Type: application/json" ${language:+-H "Accept-Language: $language"} "$@"; }
field() { python3 -I -c "import json,sys; d=json.load(open(sys.argv[1])); print(d$1)" "$WORK/lang.json" 2>/dev/null; }
template() { python3 -I - "$PROJECT" "$1" "$2" <<'PY'
import json, pathlib, sys
path = next(pathlib.Path(sys.argv[1], "src").rglob(f"messages.{sys.argv[2]}.json"))
print(json.loads(path.read_text(encoding="utf-8"))[sys.argv[3]].replace("{name}", "Product"))
PY
}
MISSING="$(new_id)"

call "$PLAIN" tr "http://localhost:$PORT/products/$MISSING"
expect_equal "languages: a failure answers in tr with Accept-Language: tr" "$(template tr not_found)" "$(field "['message']")"
call "$PLAIN" de "http://localhost:$PORT/products/$MISSING"
expect_equal "languages: with an unsupported language it answers in the default language" "$(template en not_found)" "$(field "['message']")"
call "$TR" en "http://localhost:$PORT/products/$MISSING"
expect_equal "languages: the caller's language claim comes before Accept-Language" "$(template tr not_found)" "$(field "['message']")"

call "$TR" "" -X POST -d "{\"sku\":\"L-$S\",\"name\":\"Civata-$S\",\"stock\":10}" "http://localhost:$PORT/products"
PRODUCT="$(field "['id']")"
call "$EN" "" -X PUT -d "{\"name\":\"Bolt-$S\"}" "http://localhost:$PORT/products/$PRODUCT/name"
call "$PLAIN" tr "http://localhost:$PORT/products/$PRODUCT"; IN_TR="$(field "['name']")"
call "$PLAIN" en "http://localhost:$PORT/products/$PRODUCT"; IN_EN="$(field "['name']")"
call "$PLAIN" de "http://localhost:$PORT/products/$PRODUCT"; IN_DE="$(field "['name']")"
expect_equal "languages: a name created in tr and renamed in en reads back in each, and in the default for a third" "Civata-$S|Bolt-$S|Bolt-$S" "$IN_TR|$IN_EN|$IN_DE"
expect_equal "languages: the read also carries every translation" "Civata-$S|Bolt-$S" "$(field "['names']['tr']")|$(field "['names']['en']")"

call "$TR" "" -X POST -d "{\"name\":\"Lang\",\"email\":\"lang-$S@example.com\"}" "http://localhost:$PORT/customers"
CUSTOMER="$(field "['id']")"
call "$TR" "" -X POST -d "{\"orderNumber\":\"LG-$S\",\"customerId\":$(jid "$CUSTOMER"),\"lines\":[{\"sku\":\"L-$S\",\"price\":1,\"currency\":\"EUR\",\"quantity\":1}]}" "http://localhost:$PORT/orders"
ORDER_CORRELATION="$(log_field "$PROJECT/.mortis/api.log" "l.get('Message') == 'CreateOrderCommand started' and l.get('UserId') == 'lang-tr'" CorrelationId)"
EVENTS_LOG="$PROJECT/.mortis/api.log"; [ "${WORKER:-none}" = "separate" ] && [ "${OUTBOX:-none}" != "none" ] && EVENTS_LOG="$PROJECT/.mortis/worker.log"
expect_equal "languages: the event consumer's command runs in the language of the original request" "9|1" "$(eventually "select stock from products where sku = 'L-$S'" "9" 20)|$(log_count "$EVENTS_LOG" "l.get('Message') == 'DecreaseProductStockCommand started' and l.get('CorrelationId') == '$ORDER_CORRELATION' and l.get('Language') == 'tr'")"

kill "$API_PID" 2>/dev/null
wait "$API_PID" 2>/dev/null
report
[ "$FAILED" -eq 0 ]
