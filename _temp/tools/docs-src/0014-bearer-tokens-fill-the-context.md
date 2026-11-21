# 0014 — Callers present bearer tokens; their claims fill the command's context

- Status: accepted
- Date: <the date the project was generated>
- Context: Every write records who made it, and with tenancy every row records whose it is. Before this, nothing authenticated the Api, so the audit user of a write was empty. The project must not become an identity provider.

## Decision

The Api validates JWT bearer tokens (`Microsoft.AspNetCore.Authentication.JwtBearer`) and every controller needs an authenticated caller; `/health` stays open. Keys come from an OpenID Connect authority (`Auth:Authority`) or, in Development only, a symmetric key (`Auth:SigningKey` in `appsettings.Development.json`). `ContextCommandFilter` fills `CommandContext.User` from the subject and email claims, and the tenant from the tenant claim when tenancy is on; the claim names are settings. The Console accepts the same token (`--token` or `MORTIS_TOKEN`) and otherwise runs as the console user. `dotnet run -- dev-token` prints a development token and refuses outside Development. Event consumers run as the system user.

## What varies

Which identity provider issues the tokens, and what it names the claims.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| No authentication | Nothing | Writes have no audit user; tenants cannot be told apart |
| Issue our own tokens | A user store, passwords, refresh tokens | Not this service's job |
| An API key per caller | A key table and rotation | Does not identify the user behind a call |
| **Validate tokens from an authority; claims fill the context** | One package, settings, a development key | n/a |

## Consequences

- Gained: every write records the real user; the Console and the Api read tokens the same way; local work needs no identity server.
- Lost or owed: permissions per command are not checked yet; the development key must never reach another environment.
- Cost of going back: low.

## How it is enforced

`AuthSettingsTests`, `ConsoleTokensTests`, `ContextCommandFilterTests`, and `auth.sh` (401 without or with a forged token, audit columns from the token, `dev-token` refused outside Development).
