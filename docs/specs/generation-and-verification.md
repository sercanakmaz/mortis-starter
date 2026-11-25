# How a project is generated, and how we know the skills work

Decided on 2026-11-16. Built in [phase 1, step 03](../../plans/phase-1-skills/03-scaffold-foundations.md) and proven in [phase 2](../../plans/phase-2-csharp/README.md).

## The rule

**The skills contain knowledge and checks. They do not contain a project.** When a user installs the skills there is no solution, no `.cs` or `.go` file and no template on their machine. Claude writes every file of the new project from what the skills describe, then proves the result with the checks the skills ship.

The C# solution in this repository is an **example**: the reference implementation we keep to prove that what the skills describe can be built and works. It lives outside `skills/` and is never installed.

## Layout

```
skills/                              installed on a user's machine
  mortis-architect/                  layer 1 (architecture in pseudo-code), the options, the interview, the MORTIS.md validator
  mortis-scaffold/
      SKILL.md                       the generation workflow
      references/csharp/             layer 2 for C#: how each building block and option is written
      references/go/                 layer 2 for Go
      scripts/verify.sh              build, tests, house-rule checks and the conformance suite for a generated project
      scripts/conformance/           language-neutral checks, selected by MORTIS.md
  mortis-develop/  mortis-review/

reference/template/                      temporary: the hand-written C# solution and its copy script, source material
                                     for the kernel pages; deleted at the end of phase 2

examples/                            NOT installed; only projects the skills generated and that passed
  csharp/{lean,standard,advanced}/   from phase 2
  go/...                             from phase 3

tests/
  check_docs.py                      the pages, the manifest and the shape file agree; one way per thing
  check_installable.sh               fails when anything under skills/ is project source, or an origin is named
TODO.md                              what is left and what has been written but never compiled or run (was docs/unverified.md)
docs/generation-log.md               every generation run: configuration, result, time, tokens, the fix it led to
```

## As built (phase 1, step 03)

`mortis-scaffold/scripts/`: `verify.sh` (language-neutral), `structure.py` (manifest, missing and extra files), `decisions.py` (a copy of the architect skill's parser), `lang/<lang>.sh` (tools, build, test, format, shape, rules, console, migrate, API start, wire name), `lang/csharp_shape.py`, `lang/csharp_rules.py`, `conformance/*.sh`. `references/schema.json` is a copy of the architect skill's; `tests/check_docs.py` fails when the copies differ.

## What layer 2 may contain

The finished skills hold **snippets, never whole files** (decided). A page describes one type or one concern:

- the file's path and name, and the project it belongs to;
- the **contract**: the public shape of the type (signatures, generic constraints, what it derives from);
- the **behaviour rules** in sentences (the commit order, what is retried, what must never happen);
- **a snippet for every member whose body is more than a trivial expression**. A snippet shows one member or one concern, not a file: no snippet contains a namespace or package declaration together with a complete type, and none is longer than sixty lines;
- the **tests to write** for it, by name, each with a snippet of its body and what it proves.

Because generation must pass on small models too (decided: Haiku-class and up), snippets are plentiful: a model should never have to invent the body of a kernel member.

Things that are data rather than code are written out in full because prose would be a worse copy: `Directory.Build.props`, `BannedSymbols.txt`, `.editorconfig`, `global.json`, the package list with the versions the examples were verified against, the wire-format envelope.

The only full source in the repository is under `reference/` (until phase 2 ends) and `examples/`. Nothing else in the repository may hold project source, and nothing in `skills/` ever does. `tests/check_installable.sh` enforces the file types, the snippet rules, and that no document outside `_sources/` names a codebase that was studied.

## Making runs alike

Without a template, two runs will not produce the same bytes for project-specific code. What can be made the same, and is checked, is everything a team would notice:

| Must be identical in every run | May differ |
|---|---|
| the file tree: every path and file name | the wording inside a project-specific method body |
| every type and member name, and the public shape of every kernel type | the order of private details |
| registration lines, settings keys, table and column names, routes, error bodies, wire names | |
| behaviour (the conformance suite) and the kernel's tests | |

The measures, in order of effect:

1. **A file manifest.** `references/<lang>/manifest.json` lists, per option, every file: its path with placeholders (`{Name}`, `{Aggregate}`, `{Plural}`), the type it holds and the page that describes it. The skill builds its file plan from the manifest, not from imagination. `verify.sh` expands the manifest for the project's `MORTIS.md` and fails on a missing file **and on an extra one**, so a run cannot add things nobody asked for.
2. **A shape file.** `references/<lang>/kernel-shape.txt` lists every public type and member signature of the kernel, one per line. `verify.sh` extracts the same list from the generated project and compares. A renamed parameter is a failure.
3. **Snippets for every non-trivial member**, as above, and for every kernel test. The kernel's tests are named in the manifest; `verify.sh` checks they exist and pass, which pins the behaviour of anything a snippet did not.
4. **A closed vocabulary, per language.** Layer 1 names **roles** (command, command handler, sync event handler, domain service, repository port, …), never identifiers. Each language has one naming table, `references/<lang>/naming.md`, that turns a role into a name by that language's own rules (see "Naming" below). `verify.sh` checks every type, file and folder against the table of the project's language and the naming choices in `MORTIS.md`.
5. **One way to do each thing.** A page never offers an alternative. `tests/check_docs.py` fails on wording such as "alternatively", "you may also", "or use".
6. **One complete worked slice, as snippets.** One aggregate followed from its test through the service, command, handler, controller and scenario, each file as its member snippets in order. Everything project-specific is written by imitating it.
7. **A fixed procedure with a progress file.** The skill follows numbered steps and ticks them in `.mortis/progress.md`: one layer at a time, build after each, never two layers at once. A small model handles one page and a few files per step, with only that page in view.
8. **Tools do what tools can.** Projects are created with `dotnet new` and `dotnet sln add`; the migration with `dotnet ef`; the SDK is pinned by `global.json` and packages by exact version; `dotnet format` runs last, so layout and `using` order are the tool's, not the model's.
9. **Nothing is left to choose.** Every choice is in `MORTIS.md`. Where the file is silent the manifest decides. The skill is told to stop and ask when it meets a choice neither answers.
10. **Sameness is measured, not assumed.** The generation harness runs each configuration several times and reports: paths identical (must be 100 %), kernel shape identical (must be 100 %), conformance (must be 100 %), and the share of kernel files that are identical after formatting, between runs and against the example (reported per model; the target for the current mid-size model is 90 %). A file that differs between runs points at a page that needs another snippet or a firmer rule; that page is fixed and the runs repeated.

Runs must be green on a small model and on a mid-size one. If the small model cannot get the kernel right from member snippets, the finding is reported with the failing files; the "never whole files" rule is the maintainer's to revisit, not the coding model's.

## Naming

Naming is decided in three levels, and nothing is named by taste at generation time:

| Level | Decided by | Examples |
|---|---|---|
| **Language rules** | the language; not a choice | C#: `PascalCase` types and members, `camelCase` parameters, `_camelCase` private fields, `I` prefix on interfaces, one type per file named after it. Go: exported `MixedCaps`, unexported `mixedCaps`, no `I` prefix, small interfaces named for what they do, lower-case single-word package names, `snake_case.go` file names, initialisms kept upper-case (`ID`, `HTTP`), errors named `Err…`, tests `Test…` in `_test.go` |
| **Role names** | the language's naming table | the role "handler of the command *complete order*" is `CompleteOrderCommandHandler` in `Orders/CompleteOrderCommand.cs` in C#, and `CompleteOrderHandler` in `internal/application/order/complete_order.go` in Go |
| **Project choices** | the interview, stored in `MORTIS.md` under `naming` | database names (`snake_case` or as written in code), JSON property casing (`camelCase` or `snake_case`), route style (`kebab-case` or lower-case), event names (bare `Completed` in a per-aggregate namespace, or prefixed `OrderCompleted`), test method style |

The interview asks about project choices only when the user's description suggests the defaults will not suit (an existing database, an existing API style, existing consumers). Each choice has one default per language. A choice that changes what goes on the wire (event names) says so in its option text, because consumers in other services depend on it.

## Adding a language

The design is language-pluggable. Layer 1, the options, `MORTIS.md`, the interview and the whole conformance suite know nothing about a language. A language is exactly this set, and `language` in `MORTIS.md` accepts any name for which the set exists:

```
skills/mortis-scaffold/references/<lang>/
    naming.md  manifest.json  kernel-shape.txt  worked-slice.md  one page per building block and option
skills/mortis-scaffold/scripts/lang/<lang>.sh      create projects, build, test, format, extract the public shape
skills/mortis-develop/references/<lang>/           the recipes rendered
skills/mortis-review/scripts/lang/<lang>.sh        the mechanical checks
examples/<lang>/                                   the reference examples (not installed)
```

Go (phase 3) is the first language added this way, and that phase also writes `docs/adding-a-language.md`: the checklist, in the order that worked, with the time each part took. A language may support fewer options than another; the validator reads what exists from each language's manifest and refuses the rest for that language.

## The generation workflow (what `mortis-scaffold` does)

1. Read `MORTIS.md` (if there is none, hand over to `mortis-architect`).
2. Read layer 1 for the options that are on, then the layer 2 pages for the language.
3. Write a file plan: every file to create, grouped by project, each with the reference section it comes from. Show it; in an unattended run, continue.
4. Create the solution and projects with the language's own tooling (`dotnet new`, `go mod init`), then write the files layer by layer: Shared, Domain, Application, Infrastructure, hosts, tests. Build after each layer and fix before going on.
5. Generate what must be generated by tools (the first migration).
6. Run `scripts/verify.sh`. A failure is fixed and verified again; the skill does not report success on a red check.
7. Report: what was created, the decisions that shaped it, the checks that passed, and anything it could not verify (for example no Docker) said plainly.

The business example (orders, customers, products) is itself described in layer 1 as a small domain specification, so `"example": true` is generated like everything else.

## How we know it works

Generation costs model time and tokens, so it is **not** done while the skills are being written. The work has three phases ([plans/](../../plans/README.md)):

| Phase | Generation | What is checked |
|---|---|---|
| 1. Build the skills | none (one optional pilot, only if the maintainer asks) | documents against each other and against the manifest and shape file; the validator; the snippet rules. Everything written is listed in `docs/unverified.md` because none of it has been compiled |
| 2. Generate C# with the maintainer | a few deliberate runs: the lean, standard and advanced presets and two combined variants | each generated project by `verify.sh`; each failure fixed in a skill page; each green project saved under `examples/` |
| 3. Go | the same, for Go | the same checks, unchanged |

The conformance suite is the judge. It never looks at source text; it starts the project and checks behaviour: the HTTP request table (status codes and error bodies), the Console scenarios, the rows in the database, the messages on the broker. That is why the same suite judges a C# project and a Go project.

A generation run passes when: the build has 0 warnings, the project's own tests pass, the structure, shape, naming and house-rule checks are clean, and every conformance check for its `MORTIS.md` passes. Each run records its configuration, result, time and tokens.

After phase 2, the saved examples are checked by `verify.sh` on every change. That builds and runs code but uses no model.

## What this costs, and how the plan contains it

- **Output varies between runs.** Contained by the measures above and by `verify.sh` being part of the skill: every generated project is tested on the user's machine before the skill says it is done.
- **Generation takes minutes and tokens**, where copying took a second. Kept low by generating presets, not options, and only after the skills are complete.
- **Nothing in phase 1 is proven until phase 2.** Mistakes accumulate unseen. Contained by the ledger of unverified pages, by phase 2 starting with the smallest project, and by the optional pilot.
- **A mistake in a reference page reaches every future project.** Contained by phase 2: a page is trusted only after a project generated from it passed.

## What every option step delivers (phase 1)

1. The layer 1 page (what it is, pseudo-code, rules, when to choose it).
2. The layer 2 pages as contract, rules and snippets.
3. Its entries in the manifest, the shape file and the naming table.
4. Its conformance checks, written but not yet run.
5. The decision record a generated project gets for it.
6. A row in `TODO.md` for each of the above.

No implementation and no generation. Phase 2 proves it.
