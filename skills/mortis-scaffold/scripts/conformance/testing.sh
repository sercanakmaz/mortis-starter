#!/usr/bin/env bash
# Conformance: the testing level. What each level ships, that a missing database skips rather than passes, and that each
# architecture rule fails on a deliberately broken copy of the project.
# Needs: PROJECT NAME LANG_SH WORK TESTING EXAMPLE; PGHOST PGPORT PGUSER PGPASSWORD for the integration run; dotnet.
# Run since decision 52: handlers and integration in the standard variant, architecture in the advanced variant.
# Applies when: testing is not unit (the unit branch below documents the level's shape and may run anywhere).
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

INTEGRATION="$PROJECT/test/$NAME.IntegrationTests"
case "${TESTING:-unit}" in
  unit)
    expect_equal "testing: at unit there is no integration project, no scenario and no architecture test" "no|no|no" "$([ -d "$INTEGRATION" ] && echo yes || echo no)|$([ -d "$PROJECT/test/$NAME.Tests/Scenarios" ] && echo yes || echo no)|$([ -d "$PROJECT/test/$NAME.Tests/Architecture" ] && echo yes || echo no)"
    ;;
  handlers)
    expect_equal "testing: at handlers there are scenarios but no integration project and no architecture test" "yes|no|no" "$([ -d "$PROJECT/test/$NAME.Tests/Scenarios" ] && echo yes || echo no)|$([ -d "$INTEGRATION" ] && echo yes || echo no)|$([ -d "$PROJECT/test/$NAME.Tests/Architecture" ] && echo yes || echo no)"
    ;;
esac

if [ "${TESTING:-unit}" = "integration" ] || [ "${TESTING:-unit}" = "architecture" ]; then
  expect_equal "testing: at integration and above the integration project exists" "yes" "$([ -d "$INTEGRATION" ] && echo yes || echo no)"
  (cd "$INTEGRATION" && MORTIS_TEST_POSTGRES=skip MORTIS_TEST_MONGO=skip MORTIS_TEST_RABBITMQ=skip dotnet test --no-build --logger "console;verbosity=normal" >"$WORK/testing.nodb.log" 2>&1)
  count() { local found; found="$(grep -oE "^ +$1: +[0-9]+" "$2" | grep -oE '[0-9]+' | tail -1)"; echo "${found:-0}"; }
  expect_equal "testing: with no database (MORTIS_TEST_POSTGRES or MORTIS_TEST_MONGO = skip) the integration tests are skipped, none passed and none failed" "yes|0|0" "$([ "$(count Skipped "$WORK/testing.nodb.log")" -gt 0 ] && echo yes || echo no)|$(count Passed "$WORK/testing.nodb.log")|$(count Failed "$WORK/testing.nodb.log")"
  (cd "$INTEGRATION" && MORTIS_TEST_POSTGRES="Host=${PGHOST:-localhost};Port=${PGPORT:-5432};Username=${PGUSER:-postgres};Password=${PGPASSWORD:-postgres}" MORTIS_TEST_MONGO="${MONGO_URL:-mongodb://localhost:27017/?replicaSet=rs0&directConnection=true}" MORTIS_TEST_RABBITMQ="${MORTIS_TEST_RABBITMQ:-amqp://guest:guest@localhost:5672}" dotnet test --no-build --logger "console;verbosity=normal" >"$WORK/testing.db.log" 2>&1)
  expect_equal "testing: with the database variable set (MORTIS_TEST_POSTGRES or MORTIS_TEST_MONGO) the integration tests run and pass, none skipped" "0|yes|0" "$?|$([ "$(count Passed "$WORK/testing.db.log")" -gt 0 ] && echo yes || echo no)|$(count Skipped "$WORK/testing.db.log")"
fi

if [ "${TESTING:-unit}" = "architecture" ]; then
  broken() {
    rm -rf "$WORK/broken" && mkdir -p "$WORK/broken" && cp -R "$PROJECT/." "$WORK/broken/" && rm -rf "$WORK/broken/.mortis"
  }
  run_rule() { (cd "$WORK/broken" && dotnet test "test/$NAME.Tests" -p:CollectCoverage=false --filter "FullyQualifiedName~$1" >"$WORK/testing.$1.log" 2>&1); echo $?; }

  expect_equal "testing: the architecture rules pass on the project as generated" "0" "$(cd "$PROJECT" && dotnet test "test/$NAME.Tests" -p:CollectCoverage=false --filter "FullyQualifiedName~Architecture" >"$WORK/testing.arch.log" 2>&1; echo $?)"

  expect_equal "testing: the whole unit-test run passes the line-coverage gate and reports a total" "0|yes" "$(cd "$PROJECT" && dotnet test "test/$NAME.Tests" >"$WORK/testing.coverage.log" 2>&1; echo $?)|$(grep -q '| Total' "$WORK/testing.coverage.log" && echo yes || echo no)"

  broken
  python3 -I - "$WORK/broken/test/$NAME.Tests/$NAME.Tests.csproj" <<'PY'
import re, sys
path = sys.argv[1]
text = open(path).read()
open(path, "w").write(re.sub(r"<Threshold>\d+</Threshold>", "<Threshold>100</Threshold>", text, count=1))
PY
  expect_equal "testing: raising the threshold to 100 fails the coverage gate" "fails" "$( (cd "$WORK/broken" && dotnet test "test/$NAME.Tests" >"$WORK/testing.gate.log" 2>&1) && echo passes || echo fails)"

  broken
  python3 -I - "$WORK/broken/src/$NAME.Application/ApplicationModule.cs" <<'PY'
import re, sys
path = sys.argv[1]
text = open(path).read()
open(path, "w").write(re.sub(r"\n[^\n]*AddCommandHandler<[^\n]*", "", text, count=1))
PY
  expect_equal "testing: removing one handler registration fails RegistrationRulesTests" "fails" "$([ "$(run_rule RegistrationRulesTests)" != 0 ] && echo fails || echo passes)"

  broken
  printf 'namespace %s.Application;\n\npublic class ProbeService\n{\n    public int Run() => Twice(1);\n\n    private static int Twice(int value) => value * 2;\n}\n' "$NAME" > "$WORK/broken/src/$NAME.Application/ProbeService.cs"
  expect_equal "testing: a private method in a *Service fails ServiceRulesTests" "fails" "$([ "$(run_rule ServiceRulesTests)" != 0 ] && echo fails || echo passes)"

  if [ "${EXAMPLE:-false}" = "true" ]; then
    broken
    EVENT="$(find "$WORK/broken/src" -path "*/Orders*" -name 'LineAdded.cs' -not -path "*/obj/*" | head -1)"
    python3 -I - "$EVENT" <<'PY'
import re, sys
path = sys.argv[1]
text = open(path).read()
text = re.sub(r"int Quantity\)", "[property: System.Text.Json.Serialization.JsonPropertyName(\"qty\")] int Quantity)", text, count=1)
open(path, "w").write(text)
PY
    expect_equal "testing: changing a queue event's JSON shape fails EventContractTests" "fails" "$([ "$(run_rule EventContractTests)" != 0 ] && echo fails || echo passes)"
  fi
fi

report
[ "$FAILED" -eq 0 ]
