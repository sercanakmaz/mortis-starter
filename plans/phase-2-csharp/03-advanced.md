# Phase 2, step 03 — Generate the advanced preset

Needs: step 02. With the maintainer. Needs RabbitMQ and Docker.

## Goal

The largest project: the background outbox, a separate Worker, the RabbitMQ bus, tenancy, translated data, read models, catalogued exceptions, architecture tests and observability.

## Run

"An advanced C# project on PostgreSQL and RabbitMQ named Acme, with the example."

## Checks that matter most here

- **MassTransit interoperability**: the five C# checks of `docs/specs/messaging-wire-format.md`, against a real broker with real MassTransit 8 in the test project. Every *(verify)* mark in that spec is settled here and the spec corrected.
- Two relays against one outbox table; the broker stopped and restarted.
- Tenancy: tenant A cannot reach tenant B through any path, including an event consumer in a mixed relay batch; the same key in two tenants.
- One trace across the request, the outbox, the bus and the handler.

## Acceptance

- [ ] The run passes `verify.sh`.
- [ ] The wire-format spec has no *(verify)* mark left.
- [ ] One deliberate break per option is caught.
- [ ] Fixes logged; lines removed from `docs/unverified.md`; saved as `examples/csharp/advanced/`.
