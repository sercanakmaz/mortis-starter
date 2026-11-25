# 0021 — A retried POST with the same idempotency key runs once

- Status: accepted
- Date: 2026-11-20
- Context: Clients retry a `POST` when a response is lost (a mobile network, a timeout, a queue that redelivers). Without help, each retry creates another order.

## Decision

A `POST` with an `Idempotency-Key` header is recorded in `idempotency_records`, scoped by the caller's tenant and user, with a hash of its method, path and body. A repeat with the same key and body gets the first response again, with `Idempotency-Replayed: true`; the same key with another body is a 422 `idempotency_key_reused`; a repeat while the first still runs is a 409 `idempotency_in_progress`. Only successes are stored; a failure frees the key. Records are deleted after `Idempotency:RetentionHours` (24). A `POST` without the header runs as before.

## What varies

Which clients send the header, and the retention period.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Rely on unique business keys (an order number) | Nothing extra | Only commands with a natural key are protected, and the retry gets an error instead of the first answer |
| Store the key in the command's transaction | The filter would have to live inside the pipeline | HTTP concerns would enter the application layer |
| **A resource filter and a table of keys with their responses** | One table, a cleanup, a small window after a crash | n/a |

## Consequences

- Gained: a client may retry any `POST` safely and gets the same answer.
- Lost or owed: a crash after the command committed and before the response was stored lets a retry run again after `Idempotency:InProgressTimeoutSeconds`; the command's own unique rules are the last guard. Responses are kept for the retention period.
- Cost of going back: low.

## How it is enforced

`IdempotencyFilterTests`, `IdempotencyStoreTests`, and `idempotency.sh` (a repeat returns the first response and creates one row; another body with the same key is a 422).
