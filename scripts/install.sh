#!/usr/bin/env bash
set -euo pipefail

# Installs every skill in skills/ into ~/.claude/skills (or $CLAUDE_SKILLS_DIR).
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
