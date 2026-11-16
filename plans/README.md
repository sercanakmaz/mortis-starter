# Plans

The product is **mortis-starter**: a project starter delivered as four skills (`mortis-architect`, `mortis-scaffold`, `mortis-develop`, `mortis-review`). Domain-driven design is one part of what it sets up, not the whole.

The work has three phases. Each phase has its own folder and its own README.

| Phase | Folder | What happens | C# or Go generated? |
|---|---|---|---|
| **1. Build the skills** | [phase-1-skills/](phase-1-skills/README.md) | All the knowledge is written: the architecture layer, the interview, every option, the C# language pages, the checks | **No.** Nothing is generated, compiled or run |
| **2. Generate C# with the maintainer** | [phase-2-csharp/](phase-2-csharp/README.md) | The finished skills generate real C# projects, preset by preset. The maintainer starts each run and sees each result. Failures are fixed in the skills | Yes, a few deliberate runs |
| **3. Go** | [phase-3-go/](phase-3-go/README.md) | The Go language pages are written, then Go projects are generated the same way | Yes, a few deliberate runs |

**Why generation waits until the skills are complete.** Generating a project costs model time and tokens. Doing it after every option would mean dozens of full generations of mostly the same code. So phase 1 writes everything, and phase 2 generates a small number of projects, each of which exercises many options at once.

**What that costs, and how the plan contains it.** Nothing written in phase 1 has been compiled, so mistakes accumulate until phase 2 finds them. Three things keep that manageable:
- every page and check written in phase 1 is listed in `docs/unverified.md`, so phase 2 knows exactly what has never been proven;
- phase 2 starts with the smallest project (the lean preset), so a flaw in the foundations shows up in the first run, not the last;
- the maintainer may ask for one pilot generation of the lean preset at the end of phase 1 step 03, before the other options are written. It is off unless asked for.

## The one idea to hold on to

**The skills contain knowledge and checks, never a project.** Nothing under `skills/` is project source. A user's project is written by Claude from the skills' references and judged by the skills' `verify.sh`. The existing hand-written C# solution sits in `_temp/template/` as source material for the kernel pages and is deleted at the end of phase 2. `examples/` holds only projects the skills generated and that passed, kept as proof; it is not installed either.

**Snippets, never whole files.** The finished skills may hold a snippet per member, never a complete source file. **No origins.** No file outside `_sources/` names a codebase that was studied; `_sources/` is temporary, ignored by git and deleted at the end.

## Before any step, read

1. `README.md`, `docs/decisions.md`, `docs/roadmap.md`
2. `docs/options-catalog.md`
3. `docs/specs/generation-and-verification.md` (binding for every step), `docs/specs/decisions-file.md`, `docs/specs/messaging-wire-format.md`
4. The README of the phase

## Source file names in these plans

A step file may name source files such as `UnitOfWork.cs`. Those are files of the existing C# solution in `_temp/template/assets/csharp/` (source material in phase 1), never files of the installed skills; the plans themselves are not installed either. Type names in the "Design" sections are written the C# way because C# is first; in another language the same role is named by that language's rules (`references/<lang>/naming.md`).

## Rules for every step

- **House rules of the generated code**: no comments in code; no private methods in `*Service`, `*CommandHandler`, `*EventHandler`; locals named after what they hold; a test class holds only test methods; fakes, mocks and builders live only in `TestSupport`; xUnit `Assert` only; registration is explicit, never by assembly scanning; `Guid.NewGuid()`, `DateTime.Now`, `.Result`, `.Wait()` are build errors.
- **No library where forty lines of our own do the job.** A new dependency needs a decision record.
- **One way per thing.** A reference page never offers an alternative.
- **Do not widen the step.** Anything found on the way that belongs elsewhere goes under "Found, not done" at the end of the step file.
- **Stop and ask** when an item cannot be met as written or two rules contradict.
- **Git:** work on a branch per step. Commit only when the maintainer asks; suggest the message.

## Step file layout

Goal, Read first, Design, Deliverables, then either "Acceptance" (checked in that step) or "Behaviour to prove in phase 2" (written as checks now, run later), Out of scope, Traps.
