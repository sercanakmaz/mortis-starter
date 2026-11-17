# Phase 1, step 01 — The architecture layer

Needs: nothing. Size: medium. Produces documents only. Options that do not exist yet get their own pages in steps 04 to 16.

## Goal

Write the language-neutral description of the architecture: what each layer is for, every building block in pseudo-code, how a request and an event travel, and worked scenarios. This is layer 1 of the three layers in `docs/roadmap.md`. Everything later, including the Go version, is written from it, so it must be complete enough that a developer who has never seen the C# template can decide where a piece of code belongs from these documents alone.

## Read first

The template's `docs/architecture.md`, `AGENTS.md` and `docs/adr/0001` to `0005`; the skill's `references/*.md`; `docs/options-catalog.md`.

## Design

Folder: `skills/mortis-architect/references/architecture/` (the skill's `SKILL.md` arrives in step 02).

| File | Content |
|---|---|
| `layers.md` | The four layers and the hosts. One table: layer, its single job, what it may depend on, what must never appear in it. **Shared**: the base types everything builds on, no database library. **Domain**: the business decisions; aggregates enforce their own rules, a domain service coordinates aggregates of one kind and its repository. **Application**: coordination only; a command handler validates, calls one service method, maps the result; an event handler sends one command. **Infrastructure**: implementations of what Domain and Shared declare (repositories, unit of work, bus, clock). **Hosts** (Api, Console, Worker): turn the outside world into a command and fill its context. |
| `building-blocks.md` | One section per block: Entity, AggregateRoot, child entity, value object, domain event, repository interface, domain service, command, validator, command handler, command pipeline, dispatcher, DTO, sync event handler, async event handler, unit of work, event dispatcher, host adapter. Each section: purpose in one sentence, pseudo-code, rules, the commonest mistake. |
| `request-lifecycle.md` | From host to response, numbered, including the commit order: save, sync handlers inside the transaction, commit, events leave, after-commit actions. A sequence drawn as text. |
| `events.md` | Sync versus async handlers and when each is right; the three delivery models (`none`, `after-command`, `background`) with what each can lose; at-least-once and the inbox; correlation and causation. Pseudo-code for the outbox write and the relay loop. |
| `placement-guide.md` | A lookup table: "I need to …" to layer, block and reason. At least forty rows (check a business rule, call another aggregate, send an email, read the current user, format a response, decide a retry, write a query for a list screen, …). |
| `scenarios.md` | At least eight scenarios walked through block by block, each naming the layer of every step and the wrong placement people usually pick: completing an order updates the customer (sync); adding a line moves stock (async); a blocked customer vetoes an order (sync, rollback); stock runs out under concurrency; an import of a thousand rows; a rule that needs another aggregate's data; a notification after a change; a report query. |
| `pseudo-code.md` | The notation, one page. |
| `glossary.md` | Every term used, one line each. |

Pseudo-code notation: typed, language-neutral, no keyword that belongs to one language. Layer 1 names **roles**, not identifiers: it says "the handler of the command *complete order*", and `glossary.md` lists every role. How a role is spelled is each language's naming table, which is not part of this step.

```
aggregate Order(id: OrderId)
  state: status, lines: list<OrderLine>, customerId
  operation complete(by: Actor)
    require status == Open            else BusinessRule("order.not_open")
    require lines is not empty        else BusinessRule("order.empty")
    status := Completed
    raise Completed(orderId: id, customerId, by)

command CompleteOrder(orderId) -> OrderView    [transactional, carries context]
handler CompleteOrder
  order := orders.findByIdForUpdate(command.orderId) or throw NotFound("order")
  order.complete(context.actor)
  orders.update(order)
  return OrderView.from(order)
```

## Deliverables

- The eight files above.
- `tests/check_architecture.py`: fails when a file under `architecture/` contains a language-specific term (a maintained list: `Task`, `async`, `await`, `IServiceCollection`, `DbContext`, `goroutine`, `chan`, `interface{}`, `struct`, `namespace`, `using`, `[Fact]`, …), when a building block in the list above has no section, or when a scenario does not name a layer for every step. Run it on its own; nothing in this phase builds C#.
- A line in `CHANGELOG.md`.

## Acceptance

- [ ] `python3 -I tests/check_architecture.py` passes.
- [ ] Every type in the template's `Shared` project maps to a section of `building-blocks.md` (the checker compares the two lists).
- [ ] A reader test: give a fresh model only these files and the twenty placement questions in `tests/fixtures/placement-questions.json` (write them, with the expected layer and block); at least eighteen are answered correctly. Record the run in the step's notes.
- [ ] Nothing in these files contradicts ADR 0001 to 0005.

## Out of scope

Options that do not exist yet are described only in `events.md` (delivery models) and by name elsewhere. Their own documents come with their steps.

## Traps

- Do not translate the C# code into pseudo-code line by line. Write what the block *is*; the C# is one rendering of it.
- "Application coordinates, Domain decides" must survive contact with the scenarios. If a scenario needs a rule in a handler, the scenario is showing a missing domain method.

## Notes (done)

- Eight files written; `python3 -I tests/check_architecture.py` passes; every Shared type of the template maps to a block (`tests/fixtures/shared-type-map.json`; the nine exception types and the module file map to Host adapter and Dispatcher because the step's block list has no exceptions section).
- Reader test: 19/20 on a re-test; see `docs/unverified.md`.
- Found, not done: exceptions deserve their own block (the failure family and its statuses); add in step 07. ADR 0002/0003 still describe the old user pair; revised to one `Context` in step 04.
- Revised after the first handover: cardinality made explicit (one command runs one handler and one service operation, which can raise N events; one event can have M handlers; each handler sends one command) in `events.md` ("How many of each"), `layers.md`, `building-blocks.md`, `request-lifecycle.md`, `glossary.md` and scenario 11. The placement guide now opens with a flow diagram, an eight-question decision cheat sheet and a list of common pitfalls before the fifty-row lookup table. The cheat sheet's pitfalls follow this design: a domain service coordinates one kind of aggregate and its own repository; cross-aggregate reactions are events. Reader test re-run: 19/20.
- Revised again (decision 28): the command handler is the use case; it may load and change several aggregates when the action is about all of them, and the cheat sheet has a section "A second aggregate: in the command handler or in an event handler?" with the deciding question and two tables. The domain service is optional and holds logic that fits no single aggregate (several entities, anti-corruption layers). New block: anti-corruption layer. Scenarios 1, 6, 11 reworded; 12 (transfer) and 13 (outside tax service) added. Reader test: 20/20, with one question replaced by a transfer question.
- Found, not done: the template in `_temp/template` still has one `*Service` per aggregate and the rule "a handler calls one service method" (its `AGENTS.md`, `docs/architecture.md`, ADR 0001). The C# pages written in step 03 follow decision 28, not the template; step 17 (`mortis-develop`, `mortis-review`) must check handlers by the second-aggregate test, not by counting calls.
