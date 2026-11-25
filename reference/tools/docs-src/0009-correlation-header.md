# 0009 — A caller's correlation id is honoured and returned

- Status: accepted
- Date: <the date the project was generated>
- Context: Every command and event carries a correlation id (0007). A caller that already has one (a front end, another service) must be able to pass it in and find every log line of what it caused.

## Decision

`CorrelationIdMiddleware` reads `X-Correlation-Id`; a valid `Guid` is used, anything else is ignored and a new id is made. It puts the id in `HttpContext.Items` for `ContextCommandFilter`, returns it in the response header, and opens a logging scope with it. Command, event and failure log lines carry the correlation id as a structured property.

## What varies

Where the correlation id of a request comes from.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| A new id per request | None | A caller cannot follow its own request |
| W3C trace context (OpenTelemetry) | A collector and settings | Comes with the observability add-on |
| **The `X-Correlation-Id` header, echoed** | One middleware | n/a |

## Consequences

- Gained: one id from the caller through every command, event and log line.
- Lost or owed: a caller can choose the id; it is a label, never a security boundary.
- Cost of going back: low.

## How it is enforced

`CorrelationIdMiddlewareTests` and the conformance checks of the correlation add-on.
