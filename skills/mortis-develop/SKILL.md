---
name: mortis-develop
description: Adds things to a project that mortis-scaffold generated, the way its MORTIS.md decisions require - an aggregate, a child entity, a value object, a command, a query, an event, a sync or async handler, an event consumer, a read model, a tenant-owned aggregate, a translated field, a key type change - and turns an option on later (add the outbox, tenancy, an add-on). Follows a recipe per request, test first, touching every file the change needs and nothing else, then runs verify.sh. Use whenever someone asks to add, extend, change or implement something in a Mortis project (a project with a MORTIS.md), asks "where else must this change", or wants to switch an option on after the project exists, even if they never name the skill.
---

# Develop in a Mortis project

This skill changes an existing project. It never decides architecture: `MORTIS.md` already did. It picks the recipe for the request, renders it for the project's options, writes the change test first, and proves it with `verify.sh`.

## Before anything

1. **Read `MORTIS.md`** with `python3 -I ../mortis-scaffold/scripts/decisions.py MORTIS.md get <key>` (or read the table). The values decide which lines of a recipe apply. Without a `MORTIS.md`, say so in the first line of the answer and follow the recipes as for the lean preset (`events.outbox` = `none`, `tenancy.mode` = `none`, `languages.mode` = `none`, `queries` = `commands`, `testing` = `unit`, no add-ons).
2. **Read the project's `AGENTS.md`.** Its §2 rules are the ones a recipe cites as `AGENTS n`. Where this skill and `AGENTS.md` disagree, `AGENTS.md` wins and the difference is reported.
3. **Pick the recipe** from the table below. A request that needs several (an aggregate with a child entity, a command and a consumer) runs them in the table's order, one at a time, each green before the next.

## Recipes

| Request | Recipe |
|---|---|
| A new aggregate root | `references/recipes/add-aggregate.md` |
| A child entity inside an aggregate | `references/recipes/add-child-entity.md` |
| A value object | `references/recipes/add-value-object.md` |
| A command (a write, or a read with `queries` = `commands`) | `references/recipes/add-command.md` |
| A query (with `queries` = `split` or `read-models`) | `references/recipes/add-query.md` |
| An event raised by an aggregate | `references/recipes/add-event.md` |
| A reaction that must hold together with the change | `references/recipes/add-sync-handler.md` |
| A reaction after the commit (eventual consistency) | `references/recipes/add-async-handler.md`, then `add-consumer.md` |
| An event consumer in a host | `references/recipes/add-consumer.md` |
| A read model for a screen | `references/recipes/add-read-model.md` |
| Make an aggregate belong to a tenant | `references/recipes/make-aggregate-tenant-owned.md` |
| A field shown in the caller's language | `references/recipes/add-translated-field.md` |
| Change an aggregate's key type | `references/recipes/change-a-key-type.md` |
| Turn an option on (or change one) after the project exists | `references/recipes/turn-on-an-option.md` |

## How a recipe reads

A recipe is a table of steps in order. Each row names the file to touch (C# paths; `{Name}`, `{Aggregate}`, `{Plural}` as in `naming.md`), the reference page of `mortis-scaffold` that shows how, the rule that applies, and the test that proves it. A row starting with a tag applies only when `MORTIS.md` matches it:

- `[key=value]` or `[key=v1|v2]`: the key has one of those values (`[events.outbox=after-command|background]`);
- `[key!=value]`: the key has another value (`[languages.mode!=none]`);
- `[addons=x]`: the add-on list contains `x`.

Untagged rows always apply. Rows whose tag does not match are skipped silently: a lean project's answer never mentions tenants.

## The order of work

`references/tdd-order.md` is the order inside each recipe: the fake and builder, the failing test, the code, the green test, then the next file. `references/canonical-shapes.md` is the closed list of class names and the example slice to copy shapes from. `references/where-else.md` lists, per option, the files a change usually forgets.

Rules for every change:

- Touch only the files the recipe names, plus what `where-else.md` adds for the options that are on. Name them before writing; a finding outside that list is written down, not fixed on the way.
- A consequence of an action (a second aggregate that changes because the first did) is a new event handler, never a new line in the existing command handler (decision 28). A change that is the action itself (a transfer between two accounts) stays in one command handler.
- A domain service that needs data the handler cannot know it needs gets a read port in `Domain/Ports/`, never a whole repository and never a write (decision 31).
- A fixed bug adds a row to `docs/incidents.md` with the guarding test.
- Build after every file that compiles alone; test after every recipe step that has a test.

## Finish

1. `dotnet build` with 0 warnings, `dotnet test` green.
2. A migration when the recipe changed the schema (`references/csharp/recipes.md` has the command).
3. `bash ../mortis-scaffold/scripts/verify.sh <project-dir>`: every check passes or is skipped with its reason.
4. `bash ../mortis-review/scripts/review.sh <project-dir>`: no error left.
5. Report the files touched, the tests added, and anything skipped or written down instead of fixed.

## References

| File | Read when |
|---|---|
| `references/recipes/*.md` | the request matches one |
| `references/csharp/recipes.md` | the commands, paths and C# notes of each recipe |
| `references/tdd-order.md` | always, before writing |
| `references/canonical-shapes.md` | naming a new type, or copying a shape |
| `references/where-else.md` | an option is on and the change touches what it covers |
| `../mortis-scaffold/references/<language>/` | the page a recipe row names |
| `../mortis-architect/references/architecture/placement-guide.md` | the request does not say where the code belongs |
