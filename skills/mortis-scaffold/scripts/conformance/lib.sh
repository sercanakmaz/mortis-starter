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

sql() { psql -d "$DATABASE" -tAc "$1"; }
console_run() { "$LANG_SH" console "$PROJECT" "$NAME" "$@"; }
wire_name() { "$LANG_SH" wire-name "$PROJECT" "$NAME" "$1" "$2"; }
json_field() { python3 -I -c "import sys, json; d = json.loads(sys.argv[1]); print(d$2)" "$1"; }
json_tail() { python3 -I -c "import sys; s = sys.stdin.read(); i = s.find(chr(10) + '{'); print(s[i + 1:] if i >= 0 else s[s.find('{'):])"; }

list_checks() {
  grep -oE '(expect_equal|pass|fail) "[^"]+"' "$1" | sed -E 's/^(expect_equal|pass|fail) "//; s/"$//' | awk '!seen[$0]++'
}

report() { echo "$PASSED passed, $FAILED failed"; }
