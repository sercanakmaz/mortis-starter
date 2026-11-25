# Phase 3 — Go

The same two movements as before, for a second language: first write the knowledge (no Go generated), then generate with the maintainer.

| # | File | What happens | Go generated? |
|---|---|---|---|
| 01 | [01-go-knowledge.md](01-go-knowledge.md) | The Go naming table, manifest, pages and adapter scripts are written from the architecture layer | No |
| 02 | [02-generate-go.md](02-generate-go.md) | Go projects are generated preset by preset and judged by the same `verify.sh` | Yes, a few deliberate runs |
| 03 | [03-interoperability-and-release.md](03-interoperability-and-release.md) | C# and Go services exchange events and share an outbox; release | Uses the saved projects |

Go is also the test of the claim that languages are pluggable: step 01 must not need a change to anything language-neutral, and it leaves `docs/adding-a-language.md` for the next language.
