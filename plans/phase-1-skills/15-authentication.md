# Phase 1, step 15 — Authentication and authorization (knowledge only)

Needs: phase 1 steps 04. Size: medium. Options: `auth-jwt`. No C# is written, built or generated in this step.

## Goal

The audit user of an Api write is empty today because nothing authenticates. Validate bearer tokens, fill the context from them, and let a command declare the permission it needs.

## Design

- **Authentication.** JWT bearer validation against an OpenID Connect authority (`Auth:Authority`, `Auth:Audience`). The starter does not issue tokens and is not an identity provider. For local work the compose file can start a development identity server, and a Console command prints a signed development token for a user, tenant and permissions (development signing key only, refused outside the Development environment).
- **Context.** The context filter reads the subject, email, tenant and permissions claims; the claim names are settings.
- **Authorization.** A command may implement `IAuthorizedCommand { static string Permission }`. A pipeline middleware compares it with the caller's permissions before validation and throws `ForbiddenException`. Permissions are constants declared next to the commands, one file per aggregate. Rules that depend on data ("only the owner may cancel") stay in the domain, which already receives the user.
- Every controller requires an authenticated caller unless it says otherwise.
- The system user (async handlers) and the Console user pass authorization; that is written down as a decision, not left implicit.

## Deliverables

Knowledge only. Nothing here is compiled or run in this phase.

1. **Layer 1** (`skills/mortis-architect/references/architecture/` and `references/options/`): what the option is, its pseudo-code, its rules, when to choose it and what it costs.
2. **Layer 2 for C#** (`skills/mortis-scaffold/references/csharp/`): one page per type or concern, as contract, behaviour rules, a snippet for every member with a real body, and the tests to write with their snippets. Snippets, never whole files.
3. **Entries** in `manifest.json` (every file the option adds, and which existing files it changes), `kernel-shape.txt` and `naming.md`.
4. **Conformance checks** (`skills/mortis-scaffold/scripts/conformance/`): one check per item under "Behaviour to prove in phase 2", selected by `MORTIS.md`. Written now, first run in phase 2.
5. **The decision record** a generated project gets for this option, as an outline in `references/csharp/project-docs.md` (`0011-authentication-and-permissions.md`).
6. **A line in `docs/unverified.md`** for every page and check of this step: none of it has been compiled or run yet. Phase 2 removes the lines as it proves them.
7. `tests/check_docs.py` and `tests/check_installable.sh` pass.

## Behaviour to prove in phase 2

Each item becomes a conformance check or a test snippet now. None is run in this phase; phase 2 runs them on the generated project.

- [ ] No token: 401. A token without the permission: 403 with the standard error body. With it: success, and the audit columns hold the token's subject and email (conformance, using the development token command).
- [ ] The tenant claim reaches `CommandContext.TenantId` when tenancy is on (one generation run).
- [ ] An architecture test: every writing command either implements `IAuthorizedCommand` or is on an explicit allow-list.
- [ ] The development token command refuses to run with `ASPNETCORE_ENVIRONMENT=Production`.

## Out of scope

User management, roles stored in our database, refresh tokens, API keys.
