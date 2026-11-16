#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scaffold.sh <Name> [--lang csharp] [--no-example] [--kernel-only] [--out <dir>]

  <Name>          Solution name in PascalCase, e.g. Acme or Acme.Billing
  --lang <name>   Language of the solution (default: csharp; the only one available today)
  --no-example    Leave out the Order / Customer / Product example (aggregates, commands, tests,
                  migration) and keep the empty solution
  --kernel-only   Copy only the <Name>.Shared building blocks and the build guards
                  (for adopting them inside a solution that already exists)
  --out <dir>     Parent directory of the new solution (default: current directory)

The solution is written to <dir>/<Name>. An existing non-empty target is refused.
USAGE
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSETS_DIR="$SCRIPT_DIR/../assets"

NAME=""
OUT_DIR="."
WITH_EXAMPLE=1
LANG_NAME=csharp
KERNEL_ONLY=0

while [ $# -gt 0 ]; do
  case "$1" in
    --lang)
      [ $# -ge 2 ] || { echo "error: --lang needs a language" >&2; exit 2; }
      LANG_NAME="$2"; shift ;;
    --no-example) WITH_EXAMPLE=0 ;;
    --kernel-only) KERNEL_ONLY=1 ;;
    --out)
      [ $# -ge 2 ] || { echo "error: --out needs a directory" >&2; exit 2; }
      OUT_DIR="$2"; shift ;;
    -h|--help) usage; exit 0 ;;
    -*) echo "error: unknown option $1" >&2; usage >&2; exit 2 ;;
    *)
      [ -z "$NAME" ] || { echo "error: only one <Name> is allowed (got '$NAME' and '$1')" >&2; exit 2; }
      NAME="$1" ;;
  esac
  shift
done

[ -n "$NAME" ] || { usage >&2; exit 2; }

if [ ! -d "$ASSETS_DIR/$LANG_NAME" ]; then
  echo "error: no template for language '$LANG_NAME' (available: $(cd "$ASSETS_DIR" && ls -d */ | tr -d '/' | tr '\n' ' '))" >&2
  exit 2
fi

if ! printf '%s' "$NAME" | grep -Eq '^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)*$'; then
  echo "error: '$NAME' is not a PascalCase name (examples: Acme, Acme.Billing)" >&2
  exit 2
fi

if [ "$NAME" = "Boilerplate" ]; then
  echo "error: 'Boilerplate' is the template's own placeholder name; pick another name" >&2
  exit 2
fi

command -v perl >/dev/null 2>&1 || { echo "error: perl is required (it ships with macOS and most Linux distributions)" >&2; exit 2; }

LOWER_NAME="$(printf '%s' "$NAME" | tr '[:upper:]' '[:lower:]' | tr '.' '_')"
TARGET="$OUT_DIR/$NAME"

if [ -e "$TARGET" ] && [ -n "$(ls -A "$TARGET" 2>/dev/null)" ]; then
  echo "error: $TARGET already exists and is not empty" >&2
  exit 1
fi

mkdir -p "$TARGET"

SOURCE_DIR="$ASSETS_DIR/$LANG_NAME"
EXAMPLE_MANIFEST="$ASSETS_DIR/$LANG_NAME-example.txt"

if [ "$KERNEL_ONLY" -eq 1 ]; then
  mkdir -p "$TARGET/src"
  cp -R "$SOURCE_DIR/src/Boilerplate.Shared" "$TARGET/src/"
  cp "$SOURCE_DIR/BannedSymbols.txt" "$TARGET/"
  cp "$SOURCE_DIR/Directory.Build.props" "$TARGET/Directory.Build.props.example"
  cp "$SOURCE_DIR/Directory.Packages.props" "$TARGET/Directory.Packages.props.example"
  cp "$SOURCE_DIR/.editorconfig" "$TARGET/.editorconfig.example"
else
  cp -R "$SOURCE_DIR/." "$TARGET/"

  if [ "$WITH_EXAMPLE" -eq 0 ]; then
    while read -r kind first second; do
      case "$kind" in
        delete)
          rm -rf "${TARGET:?}/$first" ;;
        lines)
          LINE_PATTERN="$first" perl -0777 -i -pe '
            my $pattern = $ENV{LINE_PATTERN};
            $_ = join("", grep { $_ !~ /$pattern/ } split(/^/m, $_));
            s/\n{3,}/\n\n/g;
            s/\{\n\n/{\n/g;
            s/\n\n(\s*\})/\n$1/g;
          ' "$TARGET/$second" ;;
        launch)
          LAUNCH_PATTERN="$first" perl -0777 -i -pe '
            my $pattern = $ENV{LAUNCH_PATTERN};
            s/    \{\n      "name": "[^"\n]*(?:$pattern)[^"\n]*".*?\n    \},?\n//gs;
            s/,(\n  \])/$1/;
          ' "$TARGET/$second" ;;
      esac
    done < "$EXAMPLE_MANIFEST"
  fi
fi

find "$TARGET" -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +

find "$TARGET" -depth -name '*Boilerplate*' | while IFS= read -r path; do
  directory="$(dirname "$path")"
  base="$(basename "$path")"
  mv "$path" "$directory/$(printf '%s' "$base" | NAME="$NAME" perl -pe 's/Boilerplate/$ENV{NAME}/g')"
done

find "$TARGET" -type f | while IFS= read -r file; do
  if LC_ALL=C grep -Iq . "$file" 2>/dev/null; then
    NAME="$NAME" LOWER_NAME="$LOWER_NAME" perl -pi -e 's/Boilerplate/$ENV{NAME}/g; s/boilerplate/$ENV{LOWER_NAME}/g' "$file"
  fi
done

echo "Created $TARGET"

if [ "$KERNEL_ONLY" -eq 1 ]; then
  cat <<NEXT
Next steps:
  1. Add src/$NAME.Shared/$NAME.Shared.csproj to your solution.
  2. Merge the three *.example files into your own build files, then delete them.
  3. Follow references/adopting-in-an-existing-project.md.
NEXT
else
  cat <<NEXT
Next steps:
  cd $TARGET
  dotnet build
  dotnet test
  docker compose up -d postgres
  dotnet ef database update --project src/$NAME.Infrastructure
NEXT
fi
