# Roadmap

The goal: a family of skills that starts a backend project by asking what the project needs, then builds exactly that, from a lean service up to a platform with several processes and many tenants. The options are in [options-catalog.md](options-catalog.md). Each step has a brief written for the model that will do the coding: [`plans/`](plans/README.md).

## Decided

| Question | Decision |
|---|---|
| How a chosen combination becomes code | Claude writes it from the skills' references. **The skills hold knowledge and checks, never a project**; nothing under `skills/` is project source. [specs/generation-and-verification.md](specs/generation-and-verification.md) |
| The name | The product and the repository are **mortis-starter**; it is installed as skills (decision 60). Domain-driven design is one part of it. The four skills are `mortis-architect`, `mortis-scaffold`, `mortis-develop`, `mortis-review`. |
| Code in the finished skills | Snippets, never whole files. |
| Which models must succeed | Small models (Haiku-class) and up. |
| Where ideas came from | Recorded only in the temporary `_sources/` folder. |
| Naming | Identifier casing follows the language (C# and Go each by their own rules). Layer 1 names roles; each language has a naming table. Database, JSON, route and event naming are project choices the interview can ask. |
| More languages | The design is language-pluggable: a language is a set of references, one adapter script per skill and its examples. Go is the first added that way and leaves a checklist for the next. |
| The existing C# template | It moves to `examples/csharp/` and becomes a reference example: it proves the skills, and is not installed on a user's machine. |
| How we know the skills work | In phase 2 a fresh model with only the installed skills must generate projects that pass the skills' own `verify.sh`; until then everything is listed as unverified. |
| Order | The architecture layer first, then C# in depth, then Go. |
| The decisions of a project | One file, `MORTIS.md`, written by the interview and read by everything else. [specs/decisions-file.md](specs/decisions-file.md) |
| Event delivery | Exactly three models: `none`, `after-command`, `background`. |
| Message bus | Our own, wire-compatible with MassTransit 8. Not MassTransit, not OpenTransit. [specs/messaging-wire-format.md](specs/messaging-wire-format.md) |
| Multi-tenancy | One shared database; an aggregate or entity opts in with `IMultiTenant`. No database or schema per tenant. |
| Who codes | The steps are prepared here; a coding model executes them one at a time from `docs/plans/`. Phases 2 and 3 are done together with the maintainer. |
| When code is generated | Only after the skills are complete (phase 2), never per option. |

## The three layers

| Layer | What it holds | Who reads it | Where |
|---|---|---|---|
| 1. Architecture | Language-neutral. What each layer is for (Application coordinates, Domain decides, Infrastructure implements, Shared is the base), every building block in pseudo-code, every option with when to choose it, worked scenarios | `mortis-architect`, and a person deciding | `skills/mortis-architect/references/` |
| 2. Language | How each building block and option is written in one language: contracts, rules, the tricky fragments, the tests to write | `mortis-scaffold`, `mortis-develop` | `skills/mortis-scaffold/references/csharp/`, `references/go/` |
| 3. Proof | Reference examples and the conformance suite. Not installed | us, to verify the skills | `examples/`, `skills/mortis-scaffold/scripts/` |

Layer 1 is the contract. An option exists in a language only when layer 1 describes it, and the Go version is written from layer 1, not from the C# code.

## Phases

| Phase | What happens | Generated code? |
|---|---|---|
| **1. Build the skills** | The architecture layer, the interview and `MORTIS.md`, the scaffold skill's foundations, the knowledge of every option (context, keys, correlation, exceptions, outbox, bus, testing, tenancy, language, MongoDB, queries, authentication, API and observability), the develop and review skills | No generation by a model. Three hand-kept reference solutions, one per preset (`reference/variants/`), are built, tested and verified at every step (decision 37) |
| **2. Generate C# with the maintainer** | The lean, standard and advanced presets and two combined variants are generated, judged by `verify.sh`, and the skills are fixed until they pass. Then sameness and small models are measured, the develop and review skills are exercised, and the plugin is packaged | A few deliberate runs |
| **3. Go** | The Go pages are written from the architecture layer, then Go projects are generated the same way; C# and Go services are shown working together | A few deliberate runs |

Generation waits until the skills are complete because each run costs model time and tokens; a few runs that each exercise many options replace dozens of runs per option. Since decision 37 the code the pages show is proven in phase 1 by the three reference solutions; what phase 2 still proves is that a model, reading only the skills, generates the same code. `docs/unverified.md` lists what neither has proven yet.

## Open questions

None blocking. Settled on 2026-11-16: one `Context` property on a command; the tenant id type is chosen in `MORTIS.md`; Go gets doc comments on exported names only; the outbox job runs inside the Api in the standard preset and in a separate Worker in the advanced one.

One risk to watch: "snippets, never whole files" together with "small models must succeed" may not both hold for the kernel. Phase 2 step 05 measures it and the maintainer decides.
