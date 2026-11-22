# 0019 — Every command declares a permission, checked against the caller's

- Status: accepted
- Date: <the date the project was generated>
- Context: Signed-in callers (`0014`) may still be allowed to do only part of what the service offers. Leaving a command unprotected by mistake must be impossible, and a reaction to an event must not do more than the user who caused it may.

## Decision

Every command implements `IAuthorizedCommand` with one permission constant, declared per aggregate next to the commands; an architecture test fails otherwise, and there is no allow-list. The caller's permissions come from a claim whose name and form the developer chose (`Auth:PermissionsClaim`, `Auth:PermissionsFormat`) and travel on the context and on every event. `CommandPipeline` checks the command's permission before validation and answers 403 `forbidden`. There is no system user and no bypass: a command sent by an event handler or consumer runs as the user whose command raised the event, with that user's permissions. The Console names the run's permissions with `--permissions`.

## What varies

Which permissions exist and who holds them; the identity provider decides the second.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Authenticated is enough | Nothing | Every signed-in caller can do everything |
| An allow-list of open commands | One list to keep | A forgotten command is open by default |
| Reactions as a system user that skips checks | A trusted actor | Audit loses the real user; a reaction may do more than its cause |
| **One permission per command, checked for every caller and every reaction** | A constant per command; reaction permissions granted with their triggers | n/a |

## Consequences

- Gained: no command is open by mistake; audit shows the real user of every write, reactions included; a refusal happens before the request's shape is checked.
- Lost or owed: a user needs the permissions of the reactions their commands trigger (creating an order needs `products.decrease-stock`); permissions travel with events, so the outbox and the bus carry them.
- Cost of going back: low.

## How it is enforced

`PermissionRulesTests`, the pipeline tests, and `permissions.sh` (403 without the permission, before validation; reactions as the user; the Console's `--permissions`).
