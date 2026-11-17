# Plans

A plan for a piece of work lives here and nowhere else: `plans/<work-name>/README.md`, in kebab-case.

A plan states, in this order:

1. **Goal**: what will be true when the work is done.
2. **Now / repro**: what happens today, or how to reproduce the bug.
3. **Expected**: the behaviour after the change, as something that can be observed.
4. **Surface**: the files that will be touched. Leaving this list later needs a reason.
5. **Constraints**: what must not change.
6. **Verification**: the test, Console scenario or request that proves it. If this line cannot be written, the work is not ready to start.
7. **End**: what closes the work.

A plan carries no commit steps. When the work ends the plan folder is deleted, and anything that should outlive it moves to `docs/`.
