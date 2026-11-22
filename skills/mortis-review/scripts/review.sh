#!/usr/bin/env bash
# Mechanical review of a Mortis project. See review.py for the checks.
#   review.sh <project-dir> [--diff <git-range>] [--format text|json]
#   review.sh <project-dir> --dry-run     list the checks and which ones run for this MORTIS.md
set -uo pipefail
exec python3 -I "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/review.py" "$@"
