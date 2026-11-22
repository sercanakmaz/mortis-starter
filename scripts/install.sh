#!/usr/bin/env bash
set -euo pipefail

# Installs every skill in skills/ into ~/.claude/skills (or $CLAUDE_SKILLS_DIR). The four skills are installed together:
# mortis-develop and mortis-review read mortis-scaffold's pages and scripts through ../mortis-scaffold.
#   install.sh          copies the skill folders
#   install.sh --link   symlinks them, so edits in this repository take effect at once

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_DIR="${CLAUDE_SKILLS_DIR:-$HOME/.claude/skills}"
MODE="copy"

case "${1:-}" in
  --link) MODE="link" ;;
  "") ;;
  *) echo "Usage: install.sh [--link]" >&2; exit 2 ;;
esac

mkdir -p "$TARGET_DIR"

for skill_dir in "$REPO_DIR"/skills/*/; do
  skill_name="$(basename "$skill_dir")"
  destination="$TARGET_DIR/$skill_name"

  if [ -e "$destination" ] || [ -L "$destination" ]; then
    backup="$destination.backup-$(date +%Y%m%d%H%M%S)"
    mv "$destination" "$backup"
    echo "Existing $skill_name moved to $backup"
  fi

  if [ "$MODE" = "link" ]; then
    ln -s "${skill_dir%/}" "$destination"
  else
    cp -R "${skill_dir%/}" "$destination"
  fi

  echo "Installed $skill_name -> $destination ($MODE)"
done

for required in mortis-architect mortis-scaffold mortis-develop mortis-review; do
  [ -e "$TARGET_DIR/$required" ] || echo "Warning: $required is missing from $TARGET_DIR; the other skills refer to it by ../$required" >&2
done
command -v python3 >/dev/null || echo "Warning: python3 is not on PATH; the skills' scripts (decisions, verify, review) need it" >&2
