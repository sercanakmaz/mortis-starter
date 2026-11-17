# The interview

The goal is a valid `MORTIS.md` with as few questions as the description allows. The preset answers almost everything; a question is asked only when the description makes a value doubtful.

## Order

1. **Read the description.** Pick the preset it fits (`references/presets.md`, "Who each is for"). Take every value the description states outright (language, database, name, "events must not be lost", "other teams consume our events", "customers share one database").
2. **Round 1, one question: the preset,** only when the description does not settle it. Options in this order: the one that fits first, marked recommended, then the others. One line each, from `presets.md`.
3. **Round 2, the slots the preset cannot guess:** `name` (propose one from the description, free text), `language` (only languages marked available in `references/schema.json`), `database`. Skip every slot the description already states.
4. **Round 3, doubtful values only,** at most four questions at a time. The preset's value is the first option, marked recommended. Each option has one line from the option page's **Costs** line. Ask a key only when a trigger below fires.
5. **Show the result:** the table, the rules that fired (the validator's notes), and every value chosen by default. Ask for one confirmation.
6. **Write the file** (see "Writing" below).

A request such as "lean, C#, PostgreSQL, called Acme" settles preset, language, database and name: no question is asked. Go straight to step 5, show the result and write it.

## Several services

When the description names several services, write `MORTIS.md` for the service of this repository only. Say that every other service gets its own repository and its own run, and list the values they must share: `events.transport`, `tenancy.mode`, `tenancy.key`, `tenancy.resolver`, `naming.events`. If the description does not say which service this repository is, ask; when nobody can answer, use the platform's name and list the question as open.

## What makes a value doubtful

| The description mentions | Ask | Page |
|---|---|---|
| events that must not be lost, payments, orders | `events.outbox`, then `events.transport` | `options/events.md` |
| other teams or services consuming events, a broker | `events.transport`, `hosts.worker` | `options/events.md`, `options/hosts.md` |
| many customers in one system, tenants, B2B with separate data | `tenancy.mode`, then `tenancy.key`, `tenancy.resolver` | `options/tenancy.md` |
| several languages, translated messages, a multilingual catalogue | `languages.mode`, `languages.supported` | `options/languages.md` |
| an existing database, API or consumers to match | the four `naming.*` keys | `options/naming.md` |
| sign-in, accounts, a token | `auth` | `options/auth.md` |
| heavy reports, list screens, dashboards | `queries` | `options/queries.md` |
| MongoDB | `keys.default` stays `guid`; note R8 | `options/database.md` |
| recurring jobs, cleanup, imports on a schedule | `addons` (scheduler), `hosts.worker` | `options/addons.md` |
| retries from clients, double submits | `addons` (idempotent-commands) | `options/addons.md` |
| nothing of the above | nothing; keep the preset | |

Identifier casing is never asked: the language decides it.

## Wording

Ask in the user's words, not the file's keys. Each option is one line: what it gives and what it costs.

- Outbox: "Events must not be lost: save them with the change and send them right after (needs a worker; handlers must tolerate repeats)" / "Keep it simple: events can be lost if the process dies at the wrong moment".
- Preset: "Prototype or small service (lean)" / "A product with one team (standard)" / "One service of a platform, with tenants and a broker (advanced)".

Never offer a combination the rules refuse. If a choice triggers a rule (for example `rabbitmq` needs an outbox and a separate worker), say which other values change and change them.

## When nobody can answer

When the session cannot ask (an unattended run, a request that says not to ask): pick the preset that fits the description, say which one **at the top of the answer**, list every value taken from the description and every default, and write the file. Do not stall.

In that run, move off the preset only for a value the description states outright. A value the description only hints at keeps the preset's value; list it under "Open questions" with the one-line cost of the alternative, so the user can decide. A guess that adds cost (tenancy, a broker, translated data) is worse than an open question.

## Writing

1. Run `python3 -I scripts/new_decisions.py --name <name> --language <language> --database <database> --preset <preset>` with one `--set key=value` for every value that differs from the preset, and one `--why key=reason` for each, using the user's reason. Add `--out <path>` for the project root.
2. Run `python3 -I scripts/validate_decisions.py <path>`. Fix what it reports and run it again. It names the line, the rule and the fix.
3. For every `--set`, write `docs/adr/NNNN-<slug>.md` from `references/adr-template.md`: context, the options with their costs (from the option page), the choice. Number from 0001.
4. Tell the user the next step: the `mortis-scaffold` skill reads `MORTIS.md` and builds the project.
