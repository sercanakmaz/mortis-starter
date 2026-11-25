#!/usr/bin/env bash
# Language adapter for Go. verify.sh calls: go.sh <action> <project-dir> <name>
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ACTION="${1:?action}"
PROJECT="${2:?project dir}"
NAME="${3:-}"
export PATH="$PATH:/usr/local/go/bin:$HOME/go/bin" GOFLAGS="${GOFLAGS:--mod=mod}" GOTOOLCHAIN="${GOTOOLCHAIN:-auto}"
BIN="$PROJECT/.mortis/bin"

case "$ACTION" in
  tools)
    command -v go >/dev/null && go version | grep -qE 'go1\.(2[4-9]|[3-9][0-9])' ;;
  build)
    mkdir -p "$BIN"
    (cd "$PROJECT" && go build ./... && go vet ./... && go build -o "$BIN/" ./cmd/... && python3 -I "$HERE/go_banned.py" "$PROJECT") >"$PROJECT/.mortis/build.log" 2>&1 ;;
  test)
    (cd "$PROJECT" && MORTIS_TEST_POSTGRES="${MORTIS_TEST_POSTGRES:-${PGHOST:+Host=$PGHOST;Port=${PGPORT:-5432};Username=${PGUSER:-postgres};Password=${PGPASSWORD:-postgres}}}" go test ./... >"$PROJECT/.mortis/test.log" 2>&1) ;;
  format)
    output="$(cd "$PROJECT" && gofmt -l .)"
    [ -z "$output" ] || { printf 'not formatted with gofmt:\n%s\n' "$output"; exit 1; } ;;
  kernel-files)
    python3 -I -c 'import json,sys; m=json.load(open(sys.argv[1])); [print(e["path"].replace("{Name}", sys.argv[2]).replace("{module}", sys.argv[2].lower())) for e in m["files"] if e["kernel"]]' "$HERE/../../references/go/manifest.json" "$NAME" ;;
  shape)
    "$0" kernel-files "$PROJECT" "$NAME" | python3 -I "$HERE/go_shape.py" "$PROJECT" "$NAME" ;;
  rules)
    python3 -I "$HERE/go_rules.py" "$PROJECT" ;;
  migrate)
    "$BIN/console" migrate 2>&1 ;;
  console)
    shift 3
    "$BIN/console" "$@" ;;
  api-start)
    port="${4:?port}"
    (cd "$PROJECT" && MORTIS_API_ADDRESS="localhost:$port" exec "$BIN/api" >"${MORTIS_LOG:-$PROJECT/.mortis/api.log}" 2>&1) &
    echo $! ;;
  worker-start)
    (cd "$PROJECT" && exec "$BIN/worker" >"${MORTIS_LOG:-$PROJECT/.mortis/worker.log}" 2>&1) &
    echo $! ;;
  wire-name)
    printf '%s.Domain.Aggregates.%s.Events:%s\n' "$NAME" "${4:?plural}" "${5:?event}" ;;
  editor-checks)
    python3 -I - "$PROJECT/.vscode/launch.json" <<'PY'
import json, sys
configurations = json.load(open(sys.argv[1]))["configurations"]
ok = any("${input:name}" in c["args"] for c in configurations)
ok = ok and all("--user" in c["args"] for c in configurations)
ok = ok and all(c["program"].rstrip("/").endswith("cmd/console") for c in configurations)
sys.exit(0 if ok else 1)
PY
    ;;
  banned-probe)
    probe="$PROJECT/internal/domain/mortis_banned_probe.go"
    printf 'package domain\n\nimport "github.com/google/uuid"\n\n// Probe makes a random id.\nfunc Probe() uuid.UUID { return uuid.New() }\n' >"$probe"
    "$0" build "$PROJECT" "$NAME"
    result=$?
    rm -f "$probe"
    "$0" build "$PROJECT" "$NAME" >/dev/null 2>&1
    [ "$result" != 0 ] ;;
  *)
    echo "unknown action $ACTION" >&2; exit 2 ;;
esac
