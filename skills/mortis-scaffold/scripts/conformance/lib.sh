#!/usr/bin/env bash
# Shared helpers for the conformance checks. Sourced by each check script, never run.
# A check script runs top to bottom. verify.sh --dry-run lists its checks with list_checks instead of running it.

PASSED=${PASSED:-0}
FAILED=${FAILED:-0}

pass() { PASSED=$((PASSED + 1)); echo "PASS  $1"; }
fail() { FAILED=$((FAILED + 1)); echo "FAIL  $1"; [ $# -gt 1 ] && echo "      $2"; }

expect_equal() {
  if [ "$2" = "$3" ]; then pass "$1"; else fail "$1" "expected '$2', got '$3'"; fi
}

sql() {
  if [ "${NAMING_DATABASE:-snake_case}" = "as-code" ]; then
    psql -d "$DATABASE" -tAc "$(psql -d "$DATABASE" -tAc "select table_name from information_schema.tables where table_schema = 'public' union select column_name from information_schema.columns where table_schema = 'public'" | python3 -I "$(dirname "${BASH_SOURCE[0]}")/as_code_sql.py" "$1")"
  else
    psql -d "$DATABASE" -tAc "$1"
  fi
}
# Every Console run names its user (CONSOLE_USER) and, with tenancy, its tenant (CONSOLE_TENANT, set by verify.sh): the Console takes no token.
# With auth = jwt every API call carries TOKEN through api_curl.
console_run() { "$LANG_SH" console "$PROJECT" "$NAME" --user "${CONSOLE_USER:-console}" --email "${CONSOLE_USER:-console}@example.com" ${CONSOLE_TENANT:+--tenant "$CONSOLE_TENANT"} "$@"; }
api_curl() { curl ${TOKEN:+-H "Authorization: Bearer $TOKEN"} "$@"; }
dev_token() { "$LANG_SH" console "$PROJECT" "$NAME" dev-token "$1" 2>/dev/null; }
new_id() { python3 -I -c 'import uuid; print(uuid.uuid4())'; }
# An id in a JSON body: a uuid is a string, a database-assigned key (keys.default = int or long) a number.
jid() { case "$1" in ''|*[!0-9]*) printf '"%s"' "$1" ;; *) printf '%s' "$1" ;; esac; }
# An id no row has, in the project's key type.
missing_id() { case "${KEYS:-guid}" in int|long) echo 999999 ;; *) echo 01900000-0000-7000-8000-000000000000 ;; esac; }
# Checks write HTTP bodies with camelCase names. api_body renames a request body's keys to naming.json;
# api_read renames a response file's keys back, so a check reads the same names whatever the project chose.
api_body() { python3 -I "$(dirname "${BASH_SOURCE[0]}")/json_names.py" out "${NAMING_JSON:-camelCase}" "$1"; }
api_read() { python3 -I "$(dirname "${BASH_SOURCE[0]}")/json_names.py" in "${NAMING_JSON:-camelCase}" "$1"; }
wire_name() { "$LANG_SH" wire-name "$PROJECT" "$NAME" "$1" "$2"; }
# The type name of an example event, by naming.events: event_name Orders Completed is Completed, or OrderCompleted when prefixed.
event_name() { if [ "${NAMING_EVENTS:-bare}" = "prefixed" ]; then printf '%s%s' "$(printf '%s' "$1" | sed -E 's/ies$/y/; t; s/s$//')" "$2"; else printf '%s' "$2"; fi; }
json_field() { python3 -I -c "import sys, json; d = json.loads(sys.argv[1]); print(d$2)" "$1"; }
# Log lines are one JSON object per line. log_count FILE EXPR counts the lines where the Python expression on l is true;
# log_field FILE EXPR FIELD prints FIELD of the first such line; error_code FILE prints the code of the Console's error line.
log_count() { python3 -I - "$1" "$2" <<'PY'
import json, sys
lines = [json.loads(line) for line in open(sys.argv[1]) if line.startswith("{")]
print(sum(1 for l in lines if eval(sys.argv[2], {}, {"l": l})))
PY
}
log_field() { python3 -I - "$1" "$2" "$3" <<'PY'
import json, sys
lines = [json.loads(line) for line in open(sys.argv[1]) if line.startswith("{")]
print(next((str(l.get(sys.argv[3])) for l in lines if eval(sys.argv[2], {}, {"l": l})), ""))
PY
}
error_code() { log_field "$1" "'code' in l" code; }
# eventually SQL EXPECTED [SECONDS]: the SQL's result once it equals EXPECTED, or its last result after SECONDS (default 5);
# for async work in the outbox models, where a retry waits for its backoff.
eventually() {
  local result
  for _ in $(seq 1 $(( ${3:-5} * 10 ))); do result="$(sql "$1")"; [ "$result" = "$2" ] && break; sleep 0.1; done
  printf '%s' "$result"
}

list_checks() {
  grep -oE '(expect_equal|pass|fail) "[^"]+"' "$1" | sed -E 's/^(expect_equal|pass|fail) "//; s/"$//' | awk '!seen[$0]++'
}

report() { echo "$PASSED passed, $FAILED failed"; }
