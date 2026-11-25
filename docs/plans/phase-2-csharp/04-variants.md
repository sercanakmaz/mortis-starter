# Phase 2, step 04 — The choices no preset covers

Needs: step 03. With the maintainer. Needs MongoDB as a replica set.

## Goal

Prove the options that the three presets leave at their defaults, with as few runs as possible by combining them.

## Runs

| Run | Combines |
|---|---|
| A | the standard preset on **MongoDB**, with tenancy using **text** tenant ids |
| B | the lean preset with **integer keys**, `naming.database` = `as-code`, `naming.json` = `snake_case`, `naming.events` = `prefixed`, and **minimal** exceptions |

Add a run only for an option that neither covers and that the maintainer wants proven now (an add-on from phase 1 step 18, for example).

## Acceptance

- [ ] Both runs pass `verify.sh`.
- [ ] Run A's Domain and Application projects are identical, file for file, to those of the PostgreSQL standard project apart from what tenancy adds.
- [ ] The conformance checks read keys, naming and tenant ids from `MORTIS.md` rather than assuming defaults (run B fails if they do not).
- [ ] Lines removed from `docs/unverified.md`. Anything still listed there is reported to the maintainer as unproven, with the reason.
