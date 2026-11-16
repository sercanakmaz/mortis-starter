# Phase 3, step 01 — The Go knowledge

Needs: phases 1 and 2. Size: very large. No Go is generated, compiled or run in this step.

## Goal

Write everything the skills need to generate Go: the naming table, the manifest, the pages for the kernel and for every option, the language adapter scripts. It is written from the language-neutral architecture layer, not translated from the C# pages, and it benefits from every fix phase 2 made to that layer.

## Read first

`mortis-architect/references/architecture/` in full; `docs/specs/generation-and-verification.md` ("Naming", "Adding a language"); the C# pages only to compare behaviour.

## Naming

Write `references/go/naming.md` first: Go's own rules (exported `MixedCaps`, unexported `mixedCaps`, no `I` prefix on interfaces, lower-case single-word packages, `snake_case.go` files, upper-case initialisms, `Err…` errors, `Test…` in `_test.go`) and the Go name of every role in the layer 1 glossary. A Go developer must recognise the result as Go; nothing is carried over from the C# table.

## Adding a language

Go is added through the language set described in the spec ("Adding a language"): its references, `scripts/lang/go.sh` for both skills, and its examples. Nothing in layer 1, the validator, `verify.sh` or the conformance suite may need a Go-specific change; if something does, that is a defect in the language-neutral part and is fixed there. Write `docs/adding-a-language.md` as you go: the checklist in the order that worked.

## Design decisions to make first (write an ADR for each before coding)

| Question | Starting position |
|---|---|
| Module layout | One Go module. `internal/shared`, `internal/domain/<aggregate>`, `internal/application/<aggregate>`, `internal/infrastructure/...`, `cmd/api`, `cmd/console`, `cmd/worker`. A package per aggregate. |
| Dependency wiring | Explicit constructors in `cmd/*/wire.go`. No container, no reflection, no code generation for wiring. |
| Dispatcher | Generics: `Send[C Command[R], R any](ctx, dispatcher, command) (R, error)` with a handler registry keyed by command type, registered explicitly. |
| Context | `CommandContext` is a field of the command, as in C#. `context.Context` carries cancellation and the transaction, never business data the domain reads. |
| Errors | The exception levels become error values: sentinel errors at `minimal`, a typed `Error{Code, Kind, Params}` at `coded`, a catalogue at `catalogued`. One HTTP mapper. No panics for domain errors. |
| Persistence | `pgx` with hand-written SQL or `sqlc`; pick one and justify it. The unit of work is a function `InTransaction(ctx, func(ctx) error)` that puts the transaction in the context for repositories. |
| Aggregates and events | An embedded `AggregateRoot` struct collects events; the unit of work drains them. Unexported fields with methods; construction through `New…` functions that validate. |
| Sync and async handlers | Two registries with the same semantics as C#: sync inside the transaction, async after the commit. |
| Validation | Plain functions per command returning field errors in the shared error shape. No tag-based validator unless an ADR argues for it. |
| HTTP | `net/http` with the standard router. |
| Migrations | SQL files and a small runner or a well-known tool; plain SQL files written by the skill. |
| Tests | The standard `testing` package, table tests, fakes and builders under `internal/testsupport`. |
| Event wire names | Declared explicitly per event (`MessageName()`), equal to the C# names in the spec. |

House rules translated, not copied. Comments (decided): an exported identifier gets the one-line doc comment Go tooling expects; nothing else is commented.


## The options, in this order

| # | Option | Phase 1 step | Go notes |
|---|---|---|---|
| 1 | command context | 04 | a struct field on the command |
| 2 | keys | 05 | `int64` identity through `RETURNING id` |
| 3 | correlation | 06 | middleware plus `slog` attributes |
| 4 | exception levels | 07 | see step 18 |
| 5 | outbox, database transport, inbox, worker | 08 | the relay is a goroutine with a ticker and graceful shutdown; `FOR UPDATE SKIP LOCKED` as in C# |
| 6 | RabbitMQ bus | 09 | `amqp091-go`; publisher confirms; the envelope by hand |
| 7 | testing levels | 10 | containers through `testcontainers-go`; architecture tests with `go/packages` import checks |
| 8 | tenancy | 11 | there is no global query filter: the repository base adds the predicate and an architecture test forbids SQL outside it |
| 9 | language | 12 | |
| 10 | MongoDB | 13 | the official driver; same replica-set requirement |
| 11 | queries | 14 | |
| 12 | authentication | 15 | |
| 13 | API conventions, observability | 16 | OpenTelemetry Go SDK |


## Deliverables

- `skills/mortis-scaffold/references/go/`: `naming.md`, `manifest.json`, `kernel-shape.txt`, `worked-slice.md`, one page per building block and option, under the same snippet rules as C#.
- `skills/mortis-scaffold/scripts/lang/go.sh` and `skills/mortis-review/scripts/lang/go.sh`; the recipes in `skills/mortis-develop/references/go/`.
- The Go decision records (one per row of the table above).
- The schema updated so the validator accepts `go` and the options the Go manifest lists.
- Every page listed in `docs/unverified.md`.
- `docs/adding-a-language.md`: the checklist in the order that worked.

## Acceptance (this phase step)

- [ ] `tests/check_docs.py` and `tests/check_installable.sh` pass for Go: every building block and option of layer 1 has a Go page, every manifest entry points at a page, no `.go` file is under `skills/`.
- [ ] A reader who knows Go and not C# can follow `references/go/` without meeting a C# term (reader test with a fresh model, as in phase 1 step 01).
- [ ] Adding Go changed no file of layer 1, the validator, `verify.sh` or the conformance suite, except to fix a language-neutral defect; each such fix is listed.

## Traps

- Translating C# idioms (a container, exceptions as panics, a base-class hierarchy) produces Go that Go developers reject. When the architecture layer and Go idiom disagree, the layer's *behaviour* wins and the *shape* is Go's.
- If something cannot be written from the architecture layer alone, the layer is incomplete: fix the layer.
