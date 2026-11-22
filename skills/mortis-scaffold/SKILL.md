---
name: mortis-scaffold
description: Generates a new backend project from its MORTIS.md decisions file, writing every file from the skill's reference pages (contracts, rules and member snippets), then proves it with verify.sh (structure, kernel shape, house rules, build, tests and behaviour checks against a real database). Also brings the building blocks into an existing solution. C# (.NET 10, EF Core, PostgreSQL) today. Use whenever the user wants to create, generate, scaffold or bootstrap a backend, API or service, has a MORTIS.md and asks to build it, asks for a .NET "starter", "boilerplate" or "clean / DDD structure", or wants an existing solution to adopt this structure, even if they never say "scaffold".
---

# Generate a project

This skill holds knowledge and checks, never a project. Every file of the new project is written from the pages in `references/<language>/`, then judged by `scripts/verify.sh`. Nothing is copied from anywhere.

## Before anything

1. **There must be a `MORTIS.md`.** If the project folder has none, hand over to the `mortis-architect` skill, which writes it. Then come back.
2. **Read the decisions:** `python3 -I scripts/decisions.py MORTIS.md get <key>` for `name`, `language`, `database`, `example`, `events.outbox`, `events.transport`, `hosts.worker`, `naming.database`. Every choice comes from `MORTIS.md`; where it is silent, the manifest decides. When a choice is answered by neither, stop and ask.
3. **Read the language's pages for what is on.** Today: `references/csharp/`. Read `naming.md` and `manifest.json` first, always.

## The procedure

Follow these steps in order. Keep `.mortis/progress.md` in the project: one line per step, ticked when its build is green. When a run is resumed, start at the first unticked step. Work on one step at a time, with only that step's page in view.

| # | Step | Page | Done when |
|---|---|---|---|
| 1 | Create the solution and projects with the tools; write the root files and project files | `solution.md` | `dotnet build` succeeds |
| 2 | Shared: domain building blocks | `shared-domain.md` | build |
| 3 | Shared: failures, persistence contracts | `shared-other.md` | build |
| 4 | Shared: commands, pipeline, dispatcher; then `SharedModule` (`shared-other.md`) | `shared-application.md` | build |
| 5 | Domain and Application project roots: `DomainModule`, `ApplicationModule` with empty bodies | `domain.md`, `application.md` | build |
| 6 | Infrastructure: logging; event publishing for `events.outbox` only (`events-inprocess.md` for `none`, `outbox.md` for `background` or `after-command`, never both), and `rabbitmq.md` when `events.transport` is `rabbitmq`; event consumer registration (`event-consumers.md`); `AuthSettings` when `auth` is `jwt` (`authentication.md`); the tenant scope when `tenancy.mode` is `interface` (`tenancy.md`); the message files and `MessageLocalizer` when `languages.mode` is not `none` (`languages.md`); persistence, unit of work (its `CommitTransactionAsync` by `events.outbox`, its `SaveChangesAsync` by `exceptions`), `PersistenceModule` (empty body) and `InfrastructureModule` | `logging.md`, `events-inprocess.md` or `outbox.md`, `rabbitmq.md`, `event-consumers.md`, `authentication.md`, `tenancy.md`, `languages.md`, `infrastructure-postgres.md`, `failures.md` | build |
| 7 | Hosts: Api and Console, the Worker when `hosts.worker` is `separate`, `ErrorReference`, VS Code files with the first launch configuration only; the add-ons' host files (`correlation.md`); the sign-in lines and `ConsoleTokens` when `auth` is `jwt` | `hosts.md`, `outbox.md`, `authentication.md`, `failures.md` | build |
| 8 | Test support and the kernel tests | `testing.md`, `testing-kernel.md` | `dotnet test` passes |
| 9 | Project documents: `AGENTS.md`, `CLAUDE.md`, `README.md`, `plans/README.md`, `docs/architecture.md`, `docs/adr/`; `MORTIS.md` stays as `mortis-architect` wrote it | `project-docs.md`, `project-docs-architecture.md`, `project-docs-adr.md` | files exist |
| 10 | Only when `example` is `true`: the example domain, in the order its page gives | `worked-slice.md`, `worked-slice-tests.md` | build and test after each line of that order |
| 11 | Only when an aggregate exists or an outbox is on: the first migration | `infrastructure-postgres.md` "Migrations" | the migration files exist |
| 12 | `dotnet format` | | no changes left |
| 13 | `scripts/verify.sh <project-dir>` | | every check passes or is skipped with a stated reason |

Rules for every step:

- Generate only what `MORTIS.md` chose (decision 33): no code for an option that is off, and no runtime switch between options.
- A registration file (`DomainModule`, `ApplicationModule`, `PersistenceModule`) is written with an empty body first and gets its lines when the types it registers exist.
- `.mortis/` is the skill's own working folder; `verify.sh` does not count it as an extra file.
- Write exactly the files `manifest.json` lists for this `MORTIS.md`, at exactly those paths. No file outside the manifest; `verify.sh` fails on missing and on extra files.
- A member with a snippet is written as the snippet shows. A member without one follows the page's rules and the nearest snippet. Never invent a member a page does not name.
- Names come from `naming.md`. The project name replaces `{Name}`; `{database}` is the name in lower case without dots.
- Build after every step. Fix the step before going on; never two steps at once.
- No comments in code. No package that is not in `solution.md`.

## Verify

```bash
bash scripts/verify.sh <project-dir>            # every check
bash scripts/verify.sh <project-dir> --dry-run  # the plan: which checks this MORTIS.md turns on
```

It checks, in order: `MORTIS.md`; the file structure against the manifest; the house rules; the kernel shape against `references/<language>/kernel-shape.txt`; the build (0 warnings); the tests; the formatter; then the conformance suite in `scripts/conformance/` for the options that are on. Conformance starts the project and checks behaviour only: Console commands, database rows, JSON log lines, HTTP status codes and error bodies. It needs PostgreSQL (`docker compose up -d postgres`); without it those checks are skipped and named as skipped.

Do not report success on a red check. Fix the cause in the project and run `verify.sh` again.

## Report

In a few lines: where the project is; the decisions that shaped it (preset and any change from it); the `verify.sh` summary line as printed; every skipped check and why; the next step (the `mortis-develop` skill for the first real aggregate).

## An existing solution

Do not generate over a solution that has code. Read `references/csharp/adopting.md` and bring the kernel in beside what exists, one aggregate at a time.

## Reference map

| File | Read it when |
|---|---|
| `references/csharp/naming.md` | always, first: every type, file and folder name |
| `references/csharp/manifest.json` | always: the file plan for this `MORTIS.md` |
| `references/csharp/kernel-shape.txt` | when `verify.sh` reports a shape difference |
| `references/csharp/solution.md` | step 1: tools, project files, pinned versions, build guards |
| `references/csharp/shared-domain.md` | step 2 |
| `references/csharp/shared-application.md` | step 4 |
| `references/csharp/shared-other.md` | steps 3 and 4 |
| `references/csharp/domain.md` | step 5, and any aggregate, event, repository interface or domain service |
| `references/csharp/application.md` | step 5, and any command, validator, handler, view or event handler |
| `references/csharp/infrastructure-postgres.md` | steps 6 and 11, and any mapping or repository |
| `references/csharp/logging.md` | step 6 |
| `references/csharp/events-inprocess.md` | step 6, when `events.outbox` is `none` |
| `references/csharp/hosts.md` | step 7, and any controller |
| `references/csharp/testing.md` | step 8: test layout, rules, kernel test support |
| `references/csharp/testing-kernel.md` | step 8: the kernel tests |
| `references/csharp/project-docs.md`, `project-docs-architecture.md`, `project-docs-adr.md` | step 9 |
| `references/csharp/worked-slice.md`, `worked-slice-tests.md` | step 10, and as the pattern for any new aggregate |
| `references/csharp/failures.md` | the `exceptions` level: what each level adds, the status table, `ErrorReference`, the catalogue |
| `references/csharp/correlation.md` | `addons` contains `correlation` |
| `references/csharp/outbox.md` | steps 6 and 7, when `events.outbox` is `background` or `after-command` |
| `references/csharp/rabbitmq.md` | steps 6 and 7, when `events.transport` is `rabbitmq` |
| `references/csharp/event-consumers.md` | steps 6 and 7: registering event consumers in the hosts, and any new consumer |
| `references/csharp/authentication.md` | steps 6 and 7, when `auth` is `jwt` |
| `references/csharp/tenancy.md` | steps 6, 7 and 10, when `tenancy.mode` is `interface` |
| `references/csharp/infrastructure-mongo.md` | steps 6 and 7, when `database` is `mongo` (not built yet: hand-written snippets) |
| `references/csharp/languages.md` | steps 3, 6, 7 and 10, when `languages.mode` is not `none` |
| `references/csharp/adopting.md` | the project already exists |
| `references/schema.json` | the keys and values of `MORTIS.md` (a copy of the architect skill's) |
| `../mortis-architect/references/architecture/` | why a block is where it is: `layers.md`, `building-blocks.md`, `placement-guide.md`, `example-domain.md` |

## Limits to state when they matter

- With `events.outbox` = `none`, an event consumer that had not run when the process died is lost.
- Error messages are English only unless `languages.mode` says otherwise, and validation field messages always are; authentication is not configured unless `auth` says so, and permissions per command are not generated yet.
- With tenancy, raw SQL and bulk updates skip the tenant filter.
- C# is the only language with references today.
