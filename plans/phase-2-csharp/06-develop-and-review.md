# Phase 2, step 06 — Exercise `mortis-develop` and `mortis-review`

Needs: step 02. With the maintainer.

## Goal

The two skills written in phase 1 step 17 are used on real generated projects.

## Runs

1. On the standard project: "add a Supplier aggregate with contacts as child entities, a command to create one, and an async handler that reacts to it."
2. On the advanced project: the same prompt. The result must be tenant-owned, with the index and tests; on the standard project it must not mention tenants.
3. On the lean project: "add the outbox to this project."
4. `review.sh` on each result, and on the planted-violation fixtures.

## Acceptance

- [ ] Each result builds with 0 warnings, its tests pass, `verify.sh` and `review.sh` are clean.
- [ ] `review.sh` reports each planted violation and nothing else.
- [ ] One run of prompt 1 **without** the skills is kept for comparison, with what it got wrong.
