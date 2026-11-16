# Phase 2, step 02 — Generate the standard preset

Needs: step 01. With the maintainer.

## Goal

One project that exercises most of the options written in phase 1 steps 04 to 08, 10, 12 (messages), 14 (split), 15 and 16.

## Run

"A standard C# project on PostgreSQL named Acme, with the example": one context on the command, correlation, coded exceptions, the outbox publishing after the command with the database as transport, the inbox, the relay inside the Api, separate queries, bearer-token authentication, translated messages, API conventions, integration tests.

## Checks that matter most here

The "Behaviour to prove in phase 2" lists of the phase 1 steps for those options. The ones most likely to fail on a first generation:
- the outbox row is written in the same transaction as the change, and a rolled-back command leaves none;
- a failed publish leaves the row for the sweeper and the request still succeeds;
- the same event delivered twice changes stock once;
- correlation and causation ids travel through a sync and an async handler;
- 401, 403 and the audit user from the token.

## Acceptance

- [ ] The run passes `verify.sh`.
- [ ] Each conformance check for these options is shown to fail once on a deliberate break (one break per option is enough).
- [ ] Fixes are in skill pages, logged; the proven lines leave `docs/unverified.md`; the project is saved as `examples/csharp/standard/`.

## Traps

A failure here may be a foundation problem that the lean run did not reach. After fixing a kernel page, re-run step 01's first run once before continuing.
