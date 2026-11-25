#!/usr/bin/env bash
# Lists the files a new aggregate needs in this project, by its MORTIS.md; with --touch, creates them empty.
#   new_aggregate.sh <project-dir> <Aggregate> <Plural> [--touch]
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(cd "${1:?usage: new_aggregate.sh <project-dir> <Aggregate> <Plural> [--touch]}" && pwd)"
AGGREGATE="${2:?aggregate name, singular PascalCase}"
PLURAL="${3:?folder name, plural PascalCase}"
TOUCH="${4:-}"
DECISIONS="$HERE/../../mortis-scaffold/scripts/decisions.py"
[ -f "$PROJECT/MORTIS.md" ] || { echo "no MORTIS.md in $PROJECT: the files below are the lean preset's" >&2; }
get() { [ -f "$PROJECT/MORTIS.md" ] && python3 -I "$DECISIONS" "$PROJECT/MORTIS.md" get "$1" 2>/dev/null || echo "${2:-}"; }
NAME="$(get name "$(basename "$PROJECT")")"
DATABASE="$(get database postgres)"
QUERIES="$(get queries commands)"
if [ "$(get language csharp)" = "go" ]; then
  LOWER="$(printf '%s' "$PLURAL" | tr 'A-Z' 'a-z')"
  SNAKE="$(printf '%s' "$AGGREGATE" | sed -E 's/([a-z0-9])([A-Z])/\1_\2/g' | tr 'A-Z' 'a-z')"
  NEXT="$(ls "$PROJECT/internal/infrastructure/persistence/migrations" 2>/dev/null | grep -cE '^[0-9]{4}_' || true)"
  FILES=(
    "internal/domain/$LOWER/$SNAKE.go"
    "internal/domain/$LOWER/constants.go"
    "internal/domain/$LOWER/events.go"
    "internal/domain/$LOWER/repository.go"
    "internal/domain/$LOWER/${SNAKE}_test.go"
    "internal/application/$LOWER/dto.go"
    "internal/application/$LOWER/create_$SNAKE.go"
    "internal/application/$LOWER/create_${SNAKE}_test.go"
    "internal/infrastructure/persistence/$LOWER/repository.go"
    "internal/infrastructure/persistence/migrations/$(printf '%04d' $((NEXT + 1)))_$LOWER.sql"
  )
  for file in "${FILES[@]}"; do
    echo "$file"
    [ "$TOUCH" = "--touch" ] && mkdir -p "$PROJECT/$(dirname "$file")" && touch "$PROJECT/$file"
  done
  echo "and lines in: internal/application/module.go (shared.Register, Repositories), internal/bootstrap/bootstrap.go, internal/api/routes.go, internal/testsupport/repositories.go (references/go/recipes.md)"
  exit 0
fi
EVENT_FILE="Created.cs"
[ "$(get naming.events bare)" = "prefixed" ] && EVENT_FILE="${AGGREGATE}Created.cs"
CONTEXT_SEGMENT=""
case ",$(get addons ""),"  in *",contexts,"*) CONTEXT_SEGMENT="Contexts/${CONTEXT:?with contexts on, set CONTEXT=<Context>}/" ;; esac

FILES=(
  "src/$NAME.Domain/${CONTEXT_SEGMENT}Aggregates/$PLURAL/$AGGREGATE.cs"
  "src/$NAME.Domain/${CONTEXT_SEGMENT}Aggregates/$PLURAL/${AGGREGATE}Constants.cs"
  "src/$NAME.Domain/${CONTEXT_SEGMENT}Aggregates/$PLURAL/I${AGGREGATE}Repository.cs"
  "src/$NAME.Domain/${CONTEXT_SEGMENT}Aggregates/$PLURAL/Events/$EVENT_FILE"
  "src/$NAME.Application/${CONTEXT_SEGMENT}$PLURAL/${AGGREGATE}Dto.cs"
  "src/$NAME.Application/${CONTEXT_SEGMENT}$PLURAL/Create${AGGREGATE}Command.cs"
  "src/$NAME.Api/Controllers/${CONTEXT_SEGMENT}${PLURAL}Controller.cs"
  "test/$NAME.Tests/TestSupport/Domain/${CONTEXT_SEGMENT}$PLURAL/Fake$AGGREGATE.cs"
  "test/$NAME.Tests/TestSupport/Domain/${CONTEXT_SEGMENT}$PLURAL/${AGGREGATE}RepositoryBuilder.cs"
  "test/$NAME.Tests/Domain/${CONTEXT_SEGMENT}$PLURAL/${AGGREGATE}Tests.cs"
  "test/$NAME.Tests/Application/${CONTEXT_SEGMENT}$PLURAL/Create${AGGREGATE}CommandHandlerTests.cs"
)
if [ "$DATABASE" = "mongo" ]; then
  FILES+=("src/$NAME.Infrastructure/Persistence/Mongo/${CONTEXT_SEGMENT}$PLURAL/${AGGREGATE}Repository.cs")
else
  FILES+=("src/$NAME.Infrastructure/Persistence/${CONTEXT_SEGMENT}$PLURAL/${AGGREGATE}Configuration.cs"
          "src/$NAME.Infrastructure/Persistence/${CONTEXT_SEGMENT}$PLURAL/${AGGREGATE}Repository.cs"
          "test/$NAME.Tests/Infrastructure/Persistence/${CONTEXT_SEGMENT}$PLURAL/${AGGREGATE}ConfigurationTests.cs"
          "test/$NAME.Tests/Infrastructure/Persistence/${CONTEXT_SEGMENT}$PLURAL/${AGGREGATE}RepositoryTests.cs")
fi
if [ "$QUERIES" != "commands" ]; then
  FILES+=("src/$NAME.Domain/${CONTEXT_SEGMENT}Aggregates/$PLURAL/I${AGGREGATE}Reader.cs"
          "src/$NAME.Domain/${CONTEXT_SEGMENT}Aggregates/$PLURAL/${AGGREGATE}View.cs"
          "src/$NAME.Application/${CONTEXT_SEGMENT}$PLURAL/Get${AGGREGATE}Query.cs")
  [ "$DATABASE" = "mongo" ] || FILES+=("src/$NAME.Infrastructure/Persistence/${CONTEXT_SEGMENT}$PLURAL/${AGGREGATE}Reader.cs")
else
  FILES+=("src/$NAME.Application/${CONTEXT_SEGMENT}$PLURAL/Get${AGGREGATE}Command.cs")
fi

for file in "${FILES[@]}"; do
  if [ "$TOUCH" = "--touch" ]; then
    if [ -e "$PROJECT/$file" ]; then echo "exists   $file"; else mkdir -p "$(dirname "$PROJECT/$file")"; : > "$PROJECT/$file"; echo "created  $file"; fi
  else
    echo "$file"
  fi
done
echo "Also: one line each in PersistenceModule.cs and ApplicationModule.cs, and a migration (references/recipes/add-aggregate.md)." >&2
