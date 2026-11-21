# Tenancy and the signed-in caller

Who is calling, which tenant's data they may touch, and how both travel through a command, its events and their consumers. The code of one language is in its pages (`authentication.md`, `tenancy.md`).

## The caller

A caller signs in elsewhere and presents a bearer token. The host checks it and fills the command's context from its claims; nothing else in the service decides who the caller is.

```
host receives a request:
  token := bearer token of the request
  not valid (signature, issuer, audience, lifetime): refuse with 401
  context.user   := (claim "sub", claim "email")         claim names are settings
  context.tenant := claim "tenant_id"                     only with tenancy
console run:
  the same, from a token given to the run; without one, the console user and no tenant
event consumer:
  context.user   := the system user
  context.tenant := event.tenant
```

- The service validates tokens and issues none. For local work a development command signs a token with a development key, and refuses outside the development environment.
- Every endpoint needs a caller, except health checks.

## Tenant-owned data

One database for every tenant. A type is tenant-owned when it says so; everything else is global and visible to every tenant.

```
Product  : Aggregate, TenantOwned       tenantId on every row, filtered and stamped
Category : Aggregate                     global
```

```
at the start of a scope (request, console run, consumed message):
  tenantScope.enter(context.tenant)       before any repository is created
command pipeline:
  command.context.tenant differs from tenantScope: refuse with 403
read of a tenant-owned type:
  no tenant in scope: refuse with 403 tenant_required, before any query
  else: only rows where tenantId = scope's tenant
save:
  new tenant-owned row: tenantId := scope's tenant
  changed or deleted row of another tenant: refuse with 403
```

- The tenant scope is the one ambient value of the design. Domain and Application never read it; they carry the tenant on the context and on every event.
- A tenant-owned read without a tenant fails. It never returns every tenant's rows.
- A child entity of a tenant-owned aggregate is tenant-owned too.
- Every unique rule on a tenant-owned table starts with the tenant (`(tenantId, sku)`), and a reference between tenant-owned tables includes it, so one tenant's row can never point at another's.
- An outbox row carries the tenant of its event; a consumer enters that tenant before it touches data. Two tenants' rows in one batch each get their own scope.
- A system job that must cross tenants (cleanup, a report) leaves the filter through one explicit, logged bypass. Raw queries and bulk updates skip the filter and need the tenant condition by hand.

## Rules

- Tenancy needs signed-in callers: the tenant is a claim of the caller, never a header the caller chooses.
- A missing tenant is 403 `tenant_required`; data of another tenant is simply not found (404), so its existence is not revealed.
- A database or schema per tenant is not offered.
