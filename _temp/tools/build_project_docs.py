from pathlib import Path
S = Path(__file__).resolve().parent / "docs-src"
T = Path(__file__).resolve().parents[1] / "variants" / "lean"
O = Path(__file__).resolve().parents[2] / "skills" / "mortis-scaffold" / "references" / "csharp"


def fence(text):
    return "~~~~markdown\n" + text.rstrip("\n").replace("Boilerplate", "{Name}").replace("boilerplate", "{database}") + "\n~~~~\n"


readme = (T / "README.md").read_text().replace("- [docs/adr/](docs/adr/): why it is built this way (the dispatcher, commands as interfaces, the user on the command, event handlers, the Console).", "- [MORTIS.md](MORTIS.md): the decisions this project was started with.\n- [docs/adr/](docs/adr/): why it is built this way (the dispatcher, commands as interfaces, the user on the command, event handlers, the Console, handlers as use cases, one context on the command).")
pd = ["# Project documents, C#", "", "The markdown files a generated project gets. They are data: write each one exactly as shown, replacing `{Name}`. Remove a line only where this page says so. `MORTIS.md` is written by `mortis-architect` and stays as it is.", "",
      "| File | Content |", "|---|---|", "| `MORTIS.md` | from `mortis-architect`, unchanged |", "| `AGENTS.md` | below |", "| `CLAUDE.md` | below |", "| `README.md` | below |", "| `plans/README.md` | below |",
      "| `docs/architecture.md` | `project-docs-architecture.md` |", "| `docs/adr/0000` to `0008`, `0010`, `0012`, `0009` with the correlation add-on, `0011` with an outbox, `0013` with RabbitMQ, `0014` with `auth` = `jwt`, `0015` with tenancy, `0016` with translated messages or data, `0017` with MongoDB, `0018` with a query side, `0019` with `auth` = `jwt`, `0020` with the api-conventions add-on, `0021` with idempotent commands, `0022` with observability | `project-docs-adr.md` |", "",
      "Without the example, remove from `README.md` and `AGENTS.md` §5 the lines that run `CreateProduct`; keep everything else.", "",
      "## AGENTS.md", "", fence((S / "AGENTS.md").read_text()), "## CLAUDE.md", "", fence((S / "CLAUDE.md").read_text()), "## README.md", "", fence(readme), "## plans/README.md", "", fence((T / "plans/README.md").read_text())]
(O / "project-docs.md").write_text("\n".join(pd))
(O / "project-docs-architecture.md").write_text("\n".join(["# Project documents: docs/architecture.md, C#", "", "Written exactly as shown, replacing `{Name}`. With `example` = `false` the examples in it still name the order domain; they illustrate and stay.", "", fence((S / "architecture.md").read_text())]))
adr = ["# Project documents: decision records, C#", "", "Folder `docs/adr/`. Written exactly as shown, replacing `{Name}`; every date is the day the project is generated. `0009` is written only when `addons` contains `correlation`; `0011` only when `events.outbox` is not `none`, with `{outbox}` replaced by its value; `0013` only when `events.transport` is `rabbitmq`; `0014` only when `auth` is `jwt`; `0015` only when `tenancy.mode` is `interface`; `0016` only when `languages.mode` is not `none`; `0017` only when `database` is `mongo` (not built yet, decision 45); `0018` only when `queries` is not `commands` (not built yet, decision 46); `0019` only when `auth` is `jwt` (not built yet, decision 47); `0020` only when `addons` contains `api-conventions`, `0021` only when it contains `idempotent-commands` and `database` is `postgres`, `0022` only when it contains `observability` (not built yet, decision 48). With an outbox, `docs/architecture.md` §3 says \"written to `outbox_messages` before COMMIT; `OutboxWorker` runs the event consumers\" in place of the `EventPublisher` line, and §8 drops the \"no outbox\" gap. A value in `MORTIS.md` that differs from its preset gets a further record from `mortis-architect`, numbered after these.", ""]
for f in sorted(S.glob("0*.md")):
    adr += [f"## {f.name}", "", fence(f.read_text())]
(O / "project-docs-adr.md").write_text("\n".join(adr))
print("project docs written")
