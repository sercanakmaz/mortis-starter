# Phase 2, step 01 — Generate the lean preset

Needs: phase 1 complete. With the maintainer.

## Goal

The first real generation. It tests the foundations everything else stands on: can a model rebuild the kernel, the layers, the hosts and the tests from snippets, and does `verify.sh` judge it correctly?

## Runs

1. "A lean C# project on PostgreSQL named Acme, with the example."
2. The same with `example` off.
3. The adoption recipe on an empty `dotnet new webapi` solution.

Use a mid-size model. Small models are step 05.

## Also in this step

- **Prove the judge.** Before trusting a green run, break the generated project three ways (remove the sync veto of the blocked customer; make the async stock handler run twice; return 500 for a not-found) and confirm a conformance check turns red each time. Break `verify.sh`'s structure checks too: one extra file, one renamed kernel parameter, one class that breaks the naming table.
- **Compare with the old solution.** The generated project and the hand-written one in `_temp/template/assets/csharp/` should have the same file tree and kernel shape. Every difference is either a gap in a page (fix the page) or an improvement (keep it, note it).
- When green, save the generated project as `examples/csharp/lean/`.

## Acceptance

- [ ] The three runs pass `verify.sh` with nothing skipped except what the machine cannot run, each named.
- [ ] The six deliberate breaks are each caught.
- [ ] Every fix made is a change to a skill page or check, listed in `docs/generation-log.md` with the failure that caused it.
- [ ] The lean lines are removed from `docs/unverified.md`.

## Traps

- If the kernel cannot be rebuilt from member snippets even on a mid-size model, stop and tell the maintainer with the failing files. Whether whole files may be shown is the maintainer's decision.
- Do not edit the generated project to get to green. A hand-fixed project proves nothing about the skill.
