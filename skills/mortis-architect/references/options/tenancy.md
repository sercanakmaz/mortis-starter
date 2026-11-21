# Tenancy

One shared database with a tenant on each row. A database or schema per tenant is not offered.

## tenancy.mode

Whether rows belong to a tenant.

**By preset:** lean `none`, standard `none`, advanced `interface`.

### none

**Choose when:** one customer, or an internal tool.

**Costs:** no isolation between customers in the data.

### interface

**Choose when:** a product sold to many customers in one database.

**Costs:** every query is filtered and every write stamped; a background job must set the tenant before it touches data; outbox rows carry the tenant; every unique rule starts with the tenant.

**Needs:** `auth` = `jwt`: the tenant is the signed-in caller's claim (`architecture/tenancy.md`).

**Rules:** R7, R13.

Example:

```
| Key | Value | Why |
| tenancy.mode | none |  |
```

## tenancy.key

The type of the tenant's id. Only when `tenancy.mode` is `interface`.

**Default:** `guid`.

### guid

**Choose when:** new systems.

**Costs:** none.

### text

**Choose when:** the tenant id is an existing code or slug.

**Costs:** longer indexes and a format to validate.

Example:

```
| Key | Value | Why |
| tenancy.key | guid |  |
```

## tenancy.resolver

Where the host reads the tenant from. Only when `tenancy.mode` is `interface`.

**Default:** `claim`.

### header

**Choose when:** service-to-service calls and tools.

**Costs:** a client can ask for any tenant unless a gateway checks it.

### claim

**Choose when:** users sign in and belong to a tenant.

**Costs:** the credential must carry the claim.

The only resolver built so far; `header` and `subdomain` are not generated yet.

### subdomain

**Choose when:** each customer has its own address.

**Costs:** wildcard names and certificates.

Example:

```
| Key | Value | Why |
| tenancy.resolver | claim |  |
```
