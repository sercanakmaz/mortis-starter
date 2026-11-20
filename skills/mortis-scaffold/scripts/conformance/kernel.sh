#!/usr/bin/env bash
# Conformance: what every project does, example or not.
# Needs: PROJECT NAME LANG_SH WORK.
# Applies when: always.
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

console_run >"$WORK/names.txt" 2>&1
expect_equal "console: with no name it exits with 1" "1" "$?"
case "$(cat "$WORK/names.txt")" in *"Usage:"*) pass "console: with no name it prints the usage" ;; *) fail "console: with no name it prints the usage" ;; esac
console_run NoSuchCommand >/dev/null 2>"$WORK/err.log"
expect_equal "console: an unknown name exits with 2 and prints invalid_parameter" "2|invalid_parameter" "$?|$(error_code "$WORK/err.log")"
"$LANG_SH" banned-probe "$PROJECT" "$NAME" && pass "a banned call (a random id) fails the build" || fail "a banned call (a random id) fails the build"

report
[ "$FAILED" -eq 0 ]
