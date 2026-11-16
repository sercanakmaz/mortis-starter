# Phase 3, step 02 — Generate Go with the maintainer

Needs: step 01. With the maintainer. Works exactly like phase 2: the same session rules, the same cost rules, the same `verify.sh` and the same conformance suite.

## Runs

| Run | Generates | Saved as |
|---|---|---|
| 1 | the lean preset in Go, with the example | `examples/go/lean/` |
| 2 | the standard preset | `examples/go/standard/` |
| 3 | the advanced preset | `examples/go/advanced/` |
| 4 | the variants: MongoDB with text tenant ids; integer keys with non-default naming | not kept unless useful |

## Acceptance

- [ ] Each run passes `verify.sh`. The conformance checks are the ones C# passed, unchanged: the same HTTP request table, the same Console scenarios, the same database assertions.
- [ ] `go vet`, `staticcheck` and `go test -race ./...` are clean in each project.
- [ ] Fixes are in the Go pages (or, when the defect is language-neutral, in layer 1, after which the affected C# run is repeated once).
- [ ] No Go line is left in `docs/unverified.md`.
