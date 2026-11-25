# Project decisions

This project was started with mortis-starter. The table below records what was chosen. Skills and checks read it; change a row here before changing the code it describes.

## Decisions

| Key | Value | Why |
|---|---|---|
| version | 1 |  |
| name | Boilerplate |  |
| language | csharp |  |
| preset | advanced |  |
| database | postgres |  |
| events.outbox | background |  |
| events.transport | rabbitmq |  |
| keys.default | guid |  |
| naming.database | snake_case |  |
| naming.json | camelCase |  |
| naming.routes | kebab-case |  |
| naming.events | bare |  |
| exceptions | catalogued |  |
| queries | read-models |  |
| tenancy.mode | interface |  |
| tenancy.key | text |  |
| tenancy.resolver | claim |  |
| languages.mode | data |  |
| languages.default | en |  |
| languages.supported | en, tr |  |
| testing | architecture |  |
| auth | jwt |  |
| hosts.worker | separate |  |
| addons | correlation, api-conventions, loop-guard, observability, idempotent-commands, soft-delete, caching, scheduler, secrets, events-project |  |
| example | true | the reference variants all carry the example domain |

## Working in this project

| To do this | Use |
|---|---|
| Add an aggregate, command, query, event or handler | the `mortis-develop` skill |
| Check a change before merging | the `mortis-review` skill |
| Turn an option on or change one | edit the table above, then ask `mortis-develop` to apply it |
| Verify the whole project | `verify.sh` from the `mortis-scaffold` skill |

The rules of the code are in [AGENTS.md](AGENTS.md); how it works is in [docs/architecture.md](docs/architecture.md); why each non-default choice was made is in [docs/adr/](docs/adr/).
