# Where else must this change

Per option, the files a change usually forgets. A change that touches what an option covers checks its row before it is reported done.

| When | A change to | Also touch |
|---|---|---|
| always | an aggregate's members | its configuration, its `Dto.Map`, its fake, a migration |
| always | a command | its validator, its `ApplicationModule` line, its handler test, the host that sends it |
| always | an event's members | every handler and consumer of it, their tests |
| `[events.outbox=after-command\|background]` | an event with a consumer | nothing in the outbox itself; the consumer must be idempotent (at-least-once) |
| `[events.transport=rabbitmq]` | a queue event's name, namespace or members | the wire name (`docs/specs/messaging-wire-format.md`, when the project has one); every other service consuming it |
| `[tenancy.mode=interface]` | a new aggregate or table | `IMultiTenant`, `tenant_id` in every unique index, a cross-tenant test |
| `[tenancy.mode=interface]` | raw SQL or a bulk update | the tenant filter by hand |
| `[languages.mode!=none]` | a new error code | every `messages.{language}.json` |
| `[languages.mode=data]` | a translated field | `LocalizedText`, its conversion in the configuration, the `Dto.Map` with the language |
| `[queries=split\|read-models]` | an aggregate's members a screen shows | the reader's projection and its view record |
| `[queries=read-models]` | an event of an aggregate a read model copies | the read model's refresh handler |
| `[auth=jwt]` | a new command | its permission constant (`permissions.md`, once permissions are generated) |
| `[testing=architecture]` | a queue event | `event-contracts.json`, rewritten deliberately with `MORTIS_UPDATE_CONTRACTS=1` |
| `[addons=caching]` | anything a cached answer shows | an event for the change and its invalidation handler |
| `[addons=soft-delete]` | a natural unique key | a filtered unique index |
| `[addons=secrets]` | a secret member | `IsSecret()` in its configuration; never filter on it |
| `[addons=contexts]` | a type another context needs | an event, never a reference |
| `[addons=events-project]` | a new queue event | it goes in `{Name}.Contracts` |
| `[addons=api-conventions]` | a partial update | `Optional<T>` members on a `PATCH` command |
| always | a bug fix | a row in `docs/incidents.md` |
