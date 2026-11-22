#!/usr/bin/env bash
# Conformance: the testing level. What each level ships, that a missing database skips rather than passes, and that each
# architecture rule fails on a deliberately broken copy of the project.
# NOT RUN YET (decision 49): written in phase 1 from testing-levels.md, first run in phase 2.
# Needs: PROJECT NAME LANG_SH WORK TESTING EXAMPLE; PGHOST PGPORT PGUSER PGPASSWORD for the integration run; dotnet.
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
  (cd "$INTEGRATION" && env -u MORTIS_TEST_POSTGRES -u MORTIS_TEST_RABBITMQ DOCKER_HOST=tcp://127.0.0.1:1 dotnet test --no-build --logger "console;verbosity=normal" >"$WORK/testing.nodb.log" 2>&1)
  expect_equal "testing: with no Docker and no MORTIS_TEST_POSTGRES the integration tests are skipped, none passed and none failed" "yes|0|0" "$(grep -qE 'Skipped: *[1-9]' "$WORK/testing.nodb.log" && echo yes || echo no)|$(grep -oE 'Passed: *[0-9]+' "$WORK/testing.nodb.log" | grep -oE '[0-9]+' | tail -1)|$(grep -oE 'Failed: *[0-9]+' "$WORK/testing.nodb.log" | grep -oE '[0-9]+' | tail -1)"
  (cd "$INTEGRATION" && MORTIS_TEST_POSTGRES="Host=${PGHOST:-localhost};Port=${PGPORT:-5432};Username=${PGUSER:-postgres};Password=${PGPASSWORD:-postgres}" MORTIS_TEST_RABBITMQ="${MORTIS_TEST_RABBITMQ:-amqp://guest:guest@localhost:5672}" dotnet test --no-build >"$WORK/testing.db.log" 2>&1)
  expect_equal "testing: with MORTIS_TEST_POSTGRES set the integration tests run and pass" "0|yes" "$?|$(grep -qE 'Passed: *[1-9]' "$WORK/testing.db.log" && echo yes || echo no)"
fi

if [ "${TESTING:-unit}" = "architecture" ]; then
  broken() {
    rm -rf "$WORK/broken" && mkdir -p "$WORK/broken" && cp -R "$PROJECT/." "$WORK/broken/" && rm -rf "$WORK/broken/.mortis"
  }
  run_rule() { (cd "$WORK/broken" && dotnet test "test/$NAME.Tests" --filter "FullyQualifiedName~$1" >"$WORK/testing.$1.log" 2>&1); echo $?; }

  expect_equal "testing: the architecture rules pass on the project as generated" "0" "$(cd "$PROJECT" && dotnet test --no-build "test/$NAME.Tests" --filter "FullyQualifiedName~Architecture" >"$WORK/testing.arch.log" 2>&1; echo $?)"

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
    EVENT="$(find "$WORK/broken/src/$NAME.Domain/Aggregates/Orders/Events" -name 'LineAdded.cs' | head -1)"
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
