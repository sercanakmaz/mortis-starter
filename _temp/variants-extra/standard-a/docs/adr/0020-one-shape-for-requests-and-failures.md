# 0020 — One shape for requests, lists and failures

- Status: accepted
- Date: 2026-11-22
- Context: A front end or another team consumes this API. Each framework failure (a body that does not parse, an unknown route) has its own default shape, a typo in a request member is silently ignored, and a partial update cannot tell "leave it" from "clear it".

## Decision

Every failure answers the error body `{ code, message, errors? }`, the framework's own included: `invalid_request` for a body that does not parse or names a member the command does not have, `route_not_found` and `method_not_allowed` for an unknown address. Unknown members are refused by default; a command that must accept them says so on its own type with `[JsonUnmappedMemberHandling(Skip)]`. `PUT` replaces; `PATCH` changes only the members present, which are `Optional<T>` so an absent member and `null` differ. Enums travel as their names. A list answers `{ items, page, size, total }`. There is one OpenAPI document and no API versioning.

## What varies

Which endpoints exist and which commands accept extra members.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| The framework's defaults | Nothing | Three error shapes; typos ignored; `null` and absent are the same |
| ProblemDetails (RFC 9457) everywhere | A second body shape next to the one commands already answer | Clients would parse both during the change |
| JSON Patch for partial updates | A patch document per request | Hard to validate against a command; clients rarely produce it |
| URL versioning (`/v1/`) from the start | A version segment and a document per version | No consumer needs a second version yet; added when a breaking change comes |
| **One error body, unknown members refused, `Optional<T>` for PATCH, no versioning** | A converter and a few conventions | n/a |

## Consequences

- Gained: a client parses one failure shape; a typo fails loudly with the member's name; `PATCH` can clear a member.
- Lost or owed: a client that sends extra members must stop or its command must opt out; the first breaking change needs a versioning decision.
- Cost of going back: low.

## How it is enforced

`ApiConventionsModuleTests`, `OptionalTests`, the `PATCH` handler tests, and `api-conventions.sh` (an unknown member is a 400 naming it and accepted by the opted-out command; an unknown route and a broken body answer the error body; `PATCH` with `null` clears and `{}` keeps).
