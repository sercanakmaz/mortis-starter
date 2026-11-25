# Phase 3, step 03 — C# and Go together, and the release

Needs: step 02.

## Goal

Show that the two languages are two renderings of one design, and ship.

## Acceptance

- [x] **Bus:** a C# service publishes and a Go service consumes, and the reverse, for the example's events, with the context headers intact; Go against real MassTransit 8 in both directions (check 6 of the wire-format spec).
- [x] **Outbox:** a C# Api and a Go Worker share one database and one outbox table: an order created through the C# Api moves stock through the Go event consumer.
- [x] `mortis-develop` and `mortis-review` are exercised on a Go project with the prompts of phase 2 step 06.
- [ ] A small-model run of the Go lean preset is measured as in phase 2 step 05, if the maintainer wants it.
- [x] The plugin is released with both languages; the README says what each language supports; `_sources/` is deleted.
