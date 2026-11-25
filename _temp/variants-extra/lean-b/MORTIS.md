# Project decisions

This project was started with mortis-starter. The table below records what was chosen. Skills and checks read it; change a row here before changing the code it describes.

## Decisions

| Key | Value | Why |
|---|---|---|
| version | 1 |  |
| name | Boilerplate |  |
| language | csharp |  |
| preset | lean |  |
| database | postgres |  |
| events.outbox | none |  |
| events.transport | in-process |  |
| keys.default | int |  |
| naming.database | as-code |  |
| naming.json | snake_case |  |
| naming.routes | kebab-case |  |
| naming.events | prefixed |  |
| exceptions | minimal |  |
| queries | commands |  |
| tenancy.mode | none |  |
| languages.mode | none |  |
| testing | unit |  |
| auth | none |  |
| hosts.worker | none |  |
| addons |  |  |
| example | true |  |

## Working in this project

| To do this | Use |
|---|---|
| Add an aggregate, command, query, event or handler | the `mortis-develop` skill |
| Check a change before merging | the `mortis-review` skill |
| Turn an option on or change one | edit the table above, then ask `mortis-develop` to apply it |
| Verify the whole project | `verify.sh` from the `mortis-scaffold` skill |

The rules of the code are in [AGENTS.md](AGENTS.md); how it works is in [docs/architecture.md](docs/architecture.md); why each non-default choice was made is in [docs/adr/](docs/adr/).
