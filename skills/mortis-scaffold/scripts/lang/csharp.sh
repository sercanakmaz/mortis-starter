#!/usr/bin/env bash
# Language adapter for C#. verify.sh calls: csharp.sh <action> <project-dir> <name>
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ACTION="${1:?action}"
PROJECT="${2:?project dir}"
NAME="${3:-}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
# The hosts run as Development unless the caller says otherwise: the development signing key lives in appsettings.Development.json.
export DOTNET_ENVIRONMENT="${DOTNET_ENVIRONMENT:-Development}" ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-${DOTNET_ENVIRONMENT:-Development}}"

case "$ACTION" in
  tools)
    version="$(dotnet --version 2>/dev/null)"
    case "$version" in 10.*) ;; *) echo "dotnet --version gave '${version:-nothing}'; the .NET SDK 10 is needed" >&2; exit 1 ;; esac ;;
  build)
    (cd "$PROJECT" && dotnet build -warnaserror >"$PROJECT/.mortis/build.log" 2>&1) && grep -q "0 Warning(s)" "$PROJECT/.mortis/build.log" ;;
  test)
    (cd "$PROJECT" && MORTIS_TEST_POSTGRES="${MORTIS_TEST_POSTGRES:-${PGHOST:+Host=$PGHOST;Port=${PGPORT:-5432};Username=${PGUSER:-postgres};Password=${PGPASSWORD:-postgres}}}" dotnet test --no-build >"$PROJECT/.mortis/test.log" 2>&1) ;;
  format)
    (cd "$PROJECT" && dotnet format --verify-no-changes >"$PROJECT/.mortis/format.log" 2>&1) ;;
  kernel-files)
    python3 -I -c 'import json,sys; m=json.load(open(sys.argv[1])); [print(e["path"].replace("{Name}", sys.argv[2])) for e in m["files"] if e["kernel"]]' "$HERE/../../references/csharp/manifest.json" "$NAME" ;;
  shape)
    "$0" kernel-files "$PROJECT" "$NAME" | python3 -I "$HERE/csharp_shape.py" "$PROJECT" "$NAME" ;;
  rules)
    python3 -I "$HERE/csharp_rules.py" "$PROJECT" ;;
  migrate)
    (cd "$PROJECT" && dotnet ef database update --project "src/$NAME.Infrastructure" 2>&1) ;;
  console)
    shift 3
    dotnet run --no-build --project "$PROJECT/src/$NAME.Console" -- "$@" ;;
  api-start)
    port="${4:?port}"
    (cd "$PROJECT/src/$NAME.Api" && ASPNETCORE_URLS="http://localhost:$port" exec dotnet run --no-build >"${MORTIS_LOG:-$PROJECT/.mortis/api.log}" 2>&1) &
    echo $! ;;
  worker-start)
    (cd "$PROJECT/src/$NAME.Worker" && exec dotnet run --no-build >"${MORTIS_LOG:-$PROJECT/.mortis/worker.log}" 2>&1) &
    echo $! ;;
  wire-name)
    EVENT="${5:?event}"
    if [ "$(python3 -I "$HERE/../decisions.py" "$PROJECT/MORTIS.md" get naming.events 2>/dev/null)" = "prefixed" ]; then
      EVENT="$(printf '%s' "${4:?plural}" | sed -E 's/ies$/y/; t; s/s$//')$EVENT"
    fi
    if [ -f "$PROJECT/src/$NAME.Contracts/${4:?plural}/$EVENT.cs" ]; then
      printf '%s.Contracts.%s:%s\n' "$NAME" "$4" "$EVENT"
    else
      printf '%s.Domain.Aggregates.%s.Events:%s\n' "$NAME" "$4" "$EVENT"
    fi ;;
  editor-checks)
    python3 -I - "$PROJECT/.vscode/launch.json" "$NAME" <<'PY'
import json, sys
data = json.load(open(sys.argv[1]))
configurations = data["configurations"]
ok = any("${input:name}" in c["args"] for c in configurations) and all(c["preLaunchTask"] == "build-console" for c in configurations)
ok = ok and all("--user" in c["args"] for c in configurations)
ok = ok and all(sys.argv[2] + ".Console.dll" in c["program"] for c in configurations)
sys.exit(0 if ok else 1)
PY
    ;;
  banned-probe)
    probe="$PROJECT/src/$NAME.Domain/MortisBannedProbe.cs"
    printf 'namespace %s.Domain;\n\npublic static class MortisBannedProbe\n{\n    public static System.Guid Id() => System.Guid.NewGuid();\n}\n' "$NAME" >"$probe"
    output="$(cd "$PROJECT" && dotnet build "src/$NAME.Domain" 2>&1)"
    rm -f "$probe"
    case "$output" in *"error RS0030"*) exit 0 ;; *) exit 1 ;; esac ;;
  *)
    echo "unknown action $ACTION" >&2; exit 2 ;;
esac
