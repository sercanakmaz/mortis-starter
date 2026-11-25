# 0015 — Tenants share one database; a type is tenant-owned by implementing IMultiTenant

- Status: accepted
- Date: <the date the project was generated>
- Context: The service is sold to many customers, and one customer must never see another's data. A database or schema per tenant multiplies migrations and connections, which is too much for a starter.

## Decision

One database for every tenant. A type that implements `IMultiTenant` has a `tenant_id` column; everything else is global. The tenant is the signed-in user's tenant claim (`0014`); the host enters it in a scoped `TenantScope`, the consumers enter the event's tenant before resolving any service, and the command pipeline checks the command's context against it. `AppDbContext` filters every tenant-owned query by the scope's tenant and stamps new rows with it; with no tenant, a tenant-owned read or write fails with 403 `tenant_required` before any SQL. Every unique index and foreign key between tenant-owned tables starts with `tenant_id`. `TenantScope.Bypass` is the only way past the filter, for system jobs, and it is logged.

## What varies

Which types belong to a tenant.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| A database or schema per tenant | Migrations and connections per tenant | Too exceptional for a starter |
| A tenant condition written in every query | Nothing up front | One forgotten condition leaks data |
| PostgreSQL row-level security only | Policies per table and a session variable | Harder to test; kept as optional hardening |
| **One interface, a query filter and a scoped tenant** | `TenantScope`, a middleware, tenant-first keys | n/a |

## Consequences

- Gained: a type becomes tenant-owned by one interface; a missing tenant fails loudly; keys stop cross-tenant references in the database.
- Lost or owed: raw SQL and bulk updates skip the filter and need the condition by hand; the scope is ambient, so a job must enter the tenant before it resolves a repository.
- Cost of going back: high, because the data is already mixed in one database.

## How it is enforced

`TenantScopeTests`, `TenantCommandHandlerMiddlewareTests`, `AppDbContextTests`, the configuration tests, and `tenancy.sh` (isolation over HTTP with two tenants, interleaved consumers, the cross-tenant foreign key, tenant-first unique indexes).
