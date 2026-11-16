# Phase 1, step 11 — Multi-tenancy by interface (knowledge only)

Needs: phase 1 steps 04, 08. Size: medium. Options: `tenancy`. No C# is written, built or generated in this step.

## Goal

A single database shared by all tenants. An aggregate or entity becomes tenant-owned by implementing one interface; everything else stays global. No database or schema per tenant (decided: that model is too exceptional for a starter).

## Design

```
interface IMultiTenant
  tenantId: TenantId           set once, never changed

Product : AggregateRoot<Guid>, IMultiTenant      -> has TenantId, filtered and stamped
Currency : AggregateRoot<int>                    -> global, visible to every tenant
```

- **Where the tenant comes from.** The host resolves it and puts it on `CommandContext.TenantId` (step 04). Api: `ITenantResolver`, with implementations for a header (`X-Tenant-Id`), a claim (`tenant_id`) and the subdomain; the project picks one in `MORTIS.md` (`tenancy.resolver`). Console: `--tenant <id>`. An event carries the tenant of the command that raised it.
- **Tenant scope.** At the start of a command the pipeline opens the tenant scope for the dependency-injection scope from `command.Context.TenantId`. This is the one ambient value in the design, and it exists because a query filter has to read it. Nothing in Domain or Application reads it; they use the context.
- **Reading.** A global query filter on every `IMultiTenant` type: `tenant_id = current`. With no tenant in scope, a query on a tenant-owned type throws `TenantRequiredException`; it does not return every tenant's rows.
- **Writing.** On save, a new `IMultiTenant` entity gets the current tenant; an entity whose tenant differs from the current one throws. A child entity of a tenant-owned aggregate is tenant-owned and gets the same id.
- **Constraints.** Every unique index on a tenant-owned table starts with `tenant_id`. A foreign key from a tenant-owned table to another tenant-owned table includes `tenant_id`, so a row cannot point at another tenant's row.
- **Events, outbox, inbox, bus.** The outbox row has `tenant_id`; the relay and the consumers restore the tenant scope **before** they resolve any service (a job that resolves a repository before setting the tenant reads the wrong data). The bus carries it in the `Mortis-Tenant-Id` header.
- **Leaving the scope on purpose.** `TenantScope.Bypass()` for system jobs (cleanup, reports across tenants), usable only from Infrastructure and the Worker, and logged.
- **Tenant id type.** Chosen in `MORTIS.md`: `tenancy.key` is `guid` (the default) or `text`. `TenantId` is a small value type in the kernel that wraps whichever was chosen, so no other code depends on the choice. Both are built and verified: `full` uses `guid`; `text` is verified by its generation runs and by the conformance checks, which take tenant ids from `MORTIS.md`.
- In `example-domain.md` (marked "when tenancy is on"): `Product`, `Customer` and `Order` become `IMultiTenant`; `Category` stays global.

Optional hardening, off by default, described in the reference: PostgreSQL row-level security policies generated for the same tables.

## Deliverables

Knowledge only. Nothing here is compiled or run in this phase.

1. **Layer 1** (`skills/mortis-architect/references/architecture/` and `references/options/`): what the option is, its pseudo-code, its rules, when to choose it and what it costs.
2. **Layer 2 for C#** (`skills/mortis-scaffold/references/csharp/`): one page per type or concern, as contract, behaviour rules, a snippet for every member with a real body, and the tests to write with their snippets. Snippets, never whole files.
3. **Entries** in `manifest.json` (every file the option adds, and which existing files it changes), `kernel-shape.txt` and `naming.md`.
4. **Conformance checks** (`skills/mortis-scaffold/scripts/conformance/`): one check per item under "Behaviour to prove in phase 2", selected by `MORTIS.md`. Written now, first run in phase 2.
5. **The decision record** a generated project gets for this option, as an outline in `references/csharp/project-docs.md` (`0009-multi-tenancy-by-interface.md`).
6. **A line in `docs/unverified.md`** for every page and check of this step: none of it has been compiled or run yet. Phase 2 removes the lines as it proves them.
7. `tests/check_docs.py` and `tests/check_installable.sh` pass.

## Behaviour to prove in phase 2

Each item becomes a conformance check or a test snippet now. None is run in this phase; phase 2 runs them on the generated project.

- [ ] Tenant A cannot read, update or delete tenant B's product through any repository method (tests and a conformance check over HTTP with two tenants).
- [ ] The same SKU can exist in two tenants; twice in one tenant is a duplicate error.
- [ ] A write to a tenant-owned aggregate with no tenant on the context fails with `TenantRequiredException` before any SQL.
- [ ] An async handler triggered by tenant A's event reads and writes only tenant A's rows, with every transport, including when two tenants' events are interleaved in one relay batch.
- [ ] An order line cannot reference another tenant's product (foreign key test against PostgreSQL).
- [ ] A global aggregate is readable from every tenant and from none.
- [ ] An architecture test (when that level is on): every `IMultiTenant` table has `tenant_id` first in each unique index.

## Out of scope

Tenant provisioning and management screens; per-tenant settings; schema or database per tenant.

## Traps

- `IgnoreQueryFilters()` anywhere outside `TenantScope.Bypass()` is a leak. Ban it in `BannedSymbols.txt` and allow it in one place.
- A relay batch holds rows of many tenants. Open a new dependency-injection scope per message; never reuse a `DbContext` across tenants.
- Raw SQL and bulk updates bypass the filter.
