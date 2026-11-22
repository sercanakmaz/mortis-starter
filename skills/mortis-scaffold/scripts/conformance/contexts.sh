#!/usr/bin/env bash
# Conformance: the contexts add-on. Code lives under Contexts/{Context}; no context refers to another's types but its events.
# NOT RUN YET (decision 50): written in phase 1 from contexts.md, first run in phase 2.
# Needs: PROJECT NAME LANG_SH WORK; dotnet.
# Applies when: addons contains contexts
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

expect_equal "contexts: the domain's aggregates live under Contexts/, none directly under Aggregates/" "yes|no" "$([ -d "$PROJECT/src/$NAME.Domain/Contexts" ] && echo yes || echo no)|$([ -d "$PROJECT/src/$NAME.Domain/Aggregates" ] && echo yes || echo no)"
expect_equal "contexts: the application mirrors the same contexts" "same" "$(diff <(ls "$PROJECT/src/$NAME.Domain/Contexts" | sort) <(ls "$PROJECT/src/$NAME.Application/Contexts" | sort) >/dev/null && echo same || echo different)"
expect_equal "contexts: ContextRulesTests exists and passes" "0" "$(cd "$PROJECT" && dotnet test --no-build "test/$NAME.Tests" --filter "FullyQualifiedName~ContextRulesTests" >"$WORK/contexts.log" 2>&1; echo $?)"

report
[ "$FAILED" -eq 0 ]
