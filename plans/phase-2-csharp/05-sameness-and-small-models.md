# Phase 2, step 05 — How alike are runs, and do small models succeed?

Needs: step 01 at least; best after step 03. With the maintainer, who sets the budget.

## Goal

Measure the two things that were decided but not yet known: that runs are alike, and that a small model can generate a passing project.

## Runs

Decide the budget with the maintainer first. The smallest useful set:

| Model | Preset | Runs |
|---|---|---|
| small | lean | 3 |
| mid-size | lean | 2 more (step 01 gave one) |
| small | standard | 1 |

## What is reported

For each model and preset: runs that passed `verify.sh`; file paths identical between runs (must be 100 %); kernel shape identical (must be 100 %); the share of kernel files identical after formatting; the files that differ most often, each with the page that describes it.

## What follows

- A file that differs between runs gets another snippet or a firmer rule in its page.
- If a small model fails on the kernel, the maintainer chooses: more and longer snippets; allowing whole kernel files in the skill; or stating that small models are not supported. This step supplies the evidence; it does not make the choice.

## Acceptance

- [ ] The report is in `docs/generation-log.md`.
- [ ] The maintainer's decision on small models is recorded in `docs/decisions.md`.
