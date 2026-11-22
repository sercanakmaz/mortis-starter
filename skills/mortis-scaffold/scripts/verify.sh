#!/usr/bin/env bash
# Verifies a generated project against its MORTIS.md.
#   verify.sh <project-dir>              run every check
#   verify.sh <project-dir> --dry-run    list the checks that would run, run nothing
# Prints PASS / FAIL / SKIP per check and "N passed, M failed, K skipped"; exit 0 only with no failure.
# Conformance needs PostgreSQL reachable through the PG* variables (default localhost:5432, postgres/postgres).
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "${1:?usage: verify.sh <project-dir> [--dry-run]}" && pwd)"
DRY_RUN=0
[ "${2:-}" = "--dry-run" ] && DRY_RUN=1

PASSED=0
FAILED=0
SKIPPED=0
SKIPS=()
pass() { PASSED=$((PASSED + 1)); echo "PASS  $1"; }
fail() { FAILED=$((FAILED + 1)); echo "FAIL  $1"; [ $# -gt 1 ] && printf '%s\n' "$2" | sed 's/^/      /'; }
skip() { SKIPPED=$((SKIPPED + 1)); SKIPS+=("$1: $2"); echo "SKIP  $1: $2"; }
plan() { echo "PLAN  $1"; }

get() { python3 -I "$HERE/decisions.py" "$PROJECT/MORTIS.md" get "$1"; }

step() {
  local name="$1"; shift
  if [ "$DRY_RUN" = 1 ]; then plan "$name"; return; fi
  local output
  if output="$("$@" 2>&1)"; then pass "$name"; else fail "$name" "$(printf '%s\n' "$output" | head -40)"; fi
}

VALIDATION="$(python3 -I "$HERE/../../mortis-architect/scripts/validate_decisions.py" "$PROJECT/MORTIS.md" 2>&1 || python3 -I "$HERE/decisions.py" "$PROJECT/MORTIS.md" get name 2>&1)"
if ! python3 -I "$HERE/decisions.py" "$PROJECT/MORTIS.md" get name >/dev/null 2>&1; then
  echo "FAIL  MORTIS.md is valid"
  printf '%s\n' "$VALIDATION" | sed 's/^/      /'
  echo "0 passed, 1 failed, 0 skipped"
  exit 1
fi
[ "$DRY_RUN" = 1 ] && plan "MORTIS.md is valid" || pass "MORTIS.md is valid"

NAME="$(get name)"
LANGUAGE="$(get language)"
EXAMPLE="$(get example)"
DATABASE_KIND="$(get database)"
EXCEPTIONS="$(get exceptions)"
OUTBOX="$(get events.outbox)"
LANG_SH="$HERE/lang/$LANGUAGE.sh"
SHAPE_FILE="$HERE/../references/$LANGUAGE/kernel-shape.txt"
mkdir -p "$PROJECT/.mortis"
WORK="$(mktemp -d)"
export PROJECT NAME LANG_SH WORK EXCEPTIONS

if [ ! -x "$LANG_SH" ]; then
  fail "the language '$LANGUAGE' has an adapter" "missing $LANG_SH"
  echo "$PASSED passed, $FAILED failed, $SKIPPED skipped"; exit 1
fi

TOOLS=1
if [ "$DRY_RUN" = 0 ] && ! "$LANG_SH" tools "$PROJECT" "$NAME"; then TOOLS=0; fi

step "structure: every file the manifest expects exists, and no other" python3 -I "$HERE/structure.py" "$PROJECT"
step "house rules: no comments, names match files, one context line per command, no writes in Domain, no private helpers in handlers, test classes hold only tests" "$LANG_SH" rules "$PROJECT" "$NAME"
shape_matches() {
  local database
  database="$(printf '%s' "$NAME" | tr 'A-Z' 'a-z' | tr -d '.')"
  diff <(python3 -I "$HERE/shape_for.py" "$SHAPE_FILE" "$PROJECT/MORTIS.md" | sed -e "s/{Name}/$NAME/g" -e "s/{database}/$database/g") <("$LANG_SH" shape "$PROJECT" "$NAME" | sed -e "s/{Name}/$NAME/g" -e "s/{database}/$database/g")
}
step "kernel shape: every public kernel type and member is exactly as in kernel-shape.txt" shape_matches
step "editor files: the launch configurations ask for the arguments and point at this project" "$LANG_SH" editor-checks "$PROJECT" "$NAME"

if [ "$TOOLS" = 0 ]; then
  for name in "build: 0 warnings, 0 errors" "tests pass" "format: the formatter changes nothing" "conformance"; do skip "$name" "the language's tools are not installed"; done
else
  step "build: 0 warnings, 0 errors" "$LANG_SH" build "$PROJECT" "$NAME"
  step "tests pass" "$LANG_SH" test "$PROJECT" "$NAME"
  step "format: the formatter changes nothing" "$LANG_SH" format "$PROJECT" "$NAME"

  . "$HERE/conformance/lib.sh"
  run_conformance() {
    local script="$HERE/conformance/$1"
    if [ "$DRY_RUN" = 1 ]; then list_checks "$script" | sed 's/^/PLAN  conformance: /'; return; fi
    local before_passed=$PASSED before_failed=$FAILED
    PASSED=0; FAILED=0
    . "$script" | sed 's/^/      /'
    local result="${PIPESTATUS[0]}"
    PASSED=$before_passed; FAILED=$before_failed
    if [ "$result" = 0 ]; then pass "conformance: $1"; else fail "conformance: $1" "see the lines above"; fi
  }
  export OUTBOX="$(get events.outbox)" TRANSPORT="$(get events.transport)" WORKER="$(get hosts.worker)" AUTH="$(get auth)" TENANCY="$(get tenancy.mode)" LANGUAGES="$(get languages.mode)"
  # With auth = jwt the checks call the API with a development token; with tenancy that token, and every Console run, belongs to TENANT_A.
  if [ "$DRY_RUN" = 0 ] && [ "$AUTH" = "jwt" ]; then
    export TENANT_A="$(new_id)" TENANT_B="$(new_id)"
    if [ "$TENANCY" = "interface" ]; then
      export TOKEN="$(dev_token "{\"subject\":\"conformance\",\"email\":\"conformance@example.com\",\"tenant\":\"$TENANT_A\"}")"
      export CONSOLE_TOKEN="$TOKEN" CONSOLE_USER="conformance"
    else
      export TOKEN="$(dev_token '{"subject":"conformance","email":"conformance@example.com"}')"
    fi
  fi
  run_conformance kernel.sh

  if [ "$EXAMPLE" = "true" ] && [ "$DATABASE_KIND" = "postgres" ]; then
    export PGHOST="${PGHOST:-localhost}" PGPORT="${PGPORT:-5432}" PGUSER="${PGUSER:-postgres}" PGPASSWORD="${PGPASSWORD:-postgres}"
    export DATABASE="mortis_verify_$(printf '%s' "$NAME" | tr 'A-Z.' 'a-z_')"
    export API_PORT="${MORTIS_API_PORT:-5099}"
    export ConnectionStrings__Default="Host=$PGHOST;Port=$PGPORT;Database=$DATABASE;Username=$PGUSER;Password=$PGPASSWORD"
    if [ "$DRY_RUN" = 0 ] && ! psql -d postgres -tAc "select 1" >/dev/null 2>&1; then
      for script in example-database.sh example-console.sh example-http.sh; do skip "conformance: $script" "PostgreSQL is not reachable at $PGHOST:$PGPORT"; done
    else
      run_conformance example-database.sh
      run_conformance example-console.sh
      if [ "$DRY_RUN" = 0 ] && [ "$WORKER" = "separate" ] && [ "$OUTBOX" != "none" ]; then
        WORKER_PID="$("$LANG_SH" worker-start "$PROJECT" "$NAME")"
        sleep 4
      fi
      run_conformance example-http.sh
    fi
  fi

  ADDONS=",$(get addons),"
  export EXAMPLE API_PORT="${API_PORT:-${MORTIS_API_PORT:-5099}}"
  case "$ADDONS" in *",correlation,"*) run_conformance addon-correlation.sh ;; esac
  if [ "$EXAMPLE" = "true" ] && [ "$DATABASE_KIND" = "postgres" ]; then
    [ "$AUTH" = "jwt" ] && run_conformance auth.sh
    [ "$TENANCY" = "interface" ] && run_conformance tenancy.sh
    [ "$LANGUAGES" != "none" ] && run_conformance languages.sh
  fi
  if [ -n "${WORKER_PID:-}" ]; then kill "$WORKER_PID" 2>/dev/null; wait "$WORKER_PID" 2>/dev/null; fi
  if [ "$EXAMPLE" = "true" ] && [ "$DATABASE_KIND" = "postgres" ] && { [ "$DRY_RUN" = 1 ] || psql -d postgres -tAc "select 1" >/dev/null 2>&1; }; then
    run_conformance outbox.sh
    if [ "$TRANSPORT" = "rabbitmq" ]; then
      if [ "$DRY_RUN" = 1 ] || (exec 3<>/dev/tcp/localhost/5672) 2>/dev/null; then
        run_conformance transport-rabbitmq.sh
      else
        skip "conformance: transport-rabbitmq.sh" "RabbitMQ is not reachable at localhost:5672"
      fi
    fi
  elif [ "$EXAMPLE" = "true" ]; then
    skip "conformance: outbox.sh" "PostgreSQL is not reachable"
  fi
fi

rm -rf "$WORK"
echo
echo "$PASSED passed, $FAILED failed, $SKIPPED skipped"
for item in "${SKIPS[@]+"${SKIPS[@]}"}"; do echo "  skipped: $item"; done
[ "$FAILED" -eq 0 ]
