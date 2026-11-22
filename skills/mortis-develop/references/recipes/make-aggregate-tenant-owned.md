# Make an aggregate tenant-owned

Every row belongs to one tenant, set from the caller and filtered on every read. Needs `tenancy.mode` = `interface` (and so `auth` = `jwt`, R13).

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | [tenancy.mode=interface] the aggregate implements `IMultiTenant` (`TenantId` with a private setter; `AppDbContext` sets it on insert) | `tenancy.md` | R13 | `AppDbContextTests`: the tenant is stamped |
| 2 | [tenancy.mode=interface] `{Aggregate}Configuration.cs`: `tenant_id` first in every unique index and alternate key | `tenancy.md` | none | the configuration test; `mortis-review` flags a unique index without it |
| 3 | [tenancy.mode=interface] the repository needs nothing: the query filter applies | `tenancy.md` | none | a cross-tenant read returns nothing |
| 4 | [tenancy.mode=interface] raw SQL in the repository adds `tenant_id = ` by hand | `tenancy.md` | none | the repository test |
| 5 | [tenancy.mode=interface] [events.outbox=after-command\|background] nothing: outbox rows already carry the tenant (R7) | `outbox.md` | R7 | `tenancy.sh` |
| 6 | [tenancy.mode=interface] [database=postgres] a migration adding `tenant_id`, with existing rows assigned to a tenant by a data step written by hand | `infrastructure-postgres.md` "Migrations" | none | `verify.sh` |
| 7 | [tenancy.mode=none] stop: turn tenancy on first with `turn-on-an-option.md` | `tenancy.md` | R13 | none |
