# Phase 2, step 07 — Package

Needs: steps 01 to 06.

## Goal

Other people can install the skills and get a working project.

## Design

- The plugin is named `mortis-starter`: a manifest and marketplace entry so the four skills install together; `scripts/install.sh` kept for manual installs; a version, a licence, `CHANGELOG.md`.
- Trigger tuning: each skill's description against should-trigger and should-not-trigger prompts.
- Continuous integration: document checks and the validator tests on every change; `verify.sh` on the saved examples on every change (this builds and runs C#, but uses no model). Generation runs are not in continuous integration; they are run by hand before a release.
- The root `README.md` rewritten for someone new: what it is, a two-minute start, the presets, what each option costs, how to add an option later.
- The repository is renamed `mortis-starter`.
- `_temp/` stays: the reference builds are the source of the pages' code and what the documentation checks compare against (decision 58). `_sources/` is deleted at release.

## Acceptance

- [x] `tests/check_installable.sh` passes on the package.
- [x] On a clean machine with only Claude Code, the .NET SDK and Docker, installing the plugin and following the README gives a running standard project (done once, recorded).
- [x] Each description triggers on at least 90 % of its should-trigger prompts and at most 10 % of its should-not prompts.
