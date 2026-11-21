import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
VARIANTS = ROOT / "_temp" / "variants"
PRESETS = ("lean", "standard", "advanced")
TEMPLATE = ROOT / "_temp" / "template" / "assets"
TARGET = ROOT / "skills" / "mortis-scaffold" / "references" / "csharp" / "manifest.json"
OUTBOX = ["after-command", "background"]
OPTION_FILES = {
    "src/Boilerplate.Api/Middlewares/CorrelationIdMiddleware.cs": {"addons": "correlation"},
    "test/Boilerplate.Tests/Api/Middlewares/CorrelationIdMiddlewareTests.cs": {"addons": "correlation"},
    "docs/adr/0009-correlation-header.md": {"addons": "correlation"},
    "src/Boilerplate.Infrastructure/Persistence/DatabaseExceptionHandler.cs": {"exceptions": ["coded", "catalogued"]},
    "src/Boilerplate.Domain/ErrorCatalogue.cs": {"exceptions": "catalogued"},
    "test/Boilerplate.Tests/Domain/ErrorCatalogueTests.cs": {"exceptions": "catalogued"},
    "src/Boilerplate.Infrastructure/Messaging/EventPublisher.cs": {"events.outbox": "none"},
    "src/Boilerplate.Infrastructure/Messaging/MessagingConstants.cs": {"events.outbox": "none"},
    "test/Boilerplate.Tests/Infrastructure/Messaging/EventPublisherTests.cs": {"events.outbox": "none"},
    "test/Boilerplate.Tests/TestSupport/Infrastructure/Messaging/EventPublisherBuilder.cs": {"events.outbox": "none"},
    "docs/adr/0011-outbox-and-publishing-models.md": {"events.outbox": OUTBOX},
    "docs/adr/0013-own-bus-compatible-with-masstransit.md": {"events.transport": "rabbitmq"},
    "src/Boilerplate.Worker/Boilerplate.Worker.csproj": {"hosts.worker": "separate"},
    "src/Boilerplate.Worker/Program.cs": {"hosts.worker": "separate"},
    "src/Boilerplate.Worker/appsettings.json": {"hosts.worker": "separate"},
    "src/Boilerplate.Infrastructure/Messaging/EventConsumerRegistry.cs": {"events.transport": ["in-process", "database"]},
}
EVENT_CONSUMER_FILES = ("src/Boilerplate.Infrastructure/Messaging/EventConsumer", "test/Boilerplate.Tests/Infrastructure/Messaging/EventConsumer")
OUTBOX_PREFIXES = ("src/Boilerplate.Infrastructure/Messaging/Outbox/", "test/Boilerplate.Tests/Infrastructure/Messaging/Outbox/")
RABBITMQ_PREFIXES = ("src/Boilerplate.Infrastructure/Messaging/RabbitMq/", "test/Boilerplate.Tests/Infrastructure/Messaging/RabbitMq/")
OPTION_PAGES = {"src/Boilerplate.Infrastructure/Persistence/DatabaseExceptionHandler.cs": "infrastructure-postgres.md", "docs/adr/0009-correlation-header.md": "project-docs-adr.md",
                "docs/adr/0011-outbox-and-publishing-models.md": "project-docs-adr.md",
                "docs/adr/0013-own-bus-compatible-with-masstransit.md": "project-docs-adr.md"}
KERNEL_PAGES = {"src/Boilerplate.Api/Middlewares/ErrorReference.cs": "failures.md"}
SKIPPED = ("MORTIS.md",)


def files_of(preset):
    root = VARIANTS / preset
    return {str(p.relative_to(root)) for p in root.rglob("*") if p.is_file() and not ({"bin", "obj", ".mortis", "Migrations"} & set(p.relative_to(root).parts)) and str(p.relative_to(root)) not in SKIPPED}


present = {preset: files_of(preset) for preset in PRESETS}
files = sorted(set().union(*present.values()))
deletes = [l.split(" ", 1)[1] for l in (TEMPLATE / "csharp-example.txt").read_text().splitlines() if l.startswith("delete ")]
deletes += ["src/Boilerplate.Application/Categories", "src/Boilerplate.Infrastructure/Persistence/Categories"]


def is_example(f):
    return any(f == d or f.startswith(d + "/") for d in deletes) or "/Categories/" in f


def page_of(f):
    if f in ("BannedSymbols.txt","Directory.Build.props","Directory.Packages.props","global.json",".editorconfig",".gitignore","Boilerplate.slnx","Dockerfile","docker-compose.yml") or f.endswith(".csproj"): return "solution.md"
    if f.startswith(".vscode/"): return "hosts.md"
    if f.startswith("docs/adr/"): return "project-docs-adr.md"
    if f == "docs/architecture.md": return "project-docs-architecture.md"
    if f.endswith(".md"): return "project-docs.md"
    if "/Migrations/" in f: return "infrastructure-postgres.md"
    if f.startswith("test/"):
        if is_example(f): return "worked-slice-tests.md"
        if "Infrastructure/Messaging/EventPublisher" in f: return "events-inprocess.md"
        return "testing.md" if "/TestSupport/" in f else "testing-kernel.md"
    if "Boilerplate.Shared/Domain" in f: return "shared-domain.md"
    if "Boilerplate.Shared/Application" in f: return "shared-application.md"
    if "Boilerplate.Shared/" in f: return "shared-other.md"
    if "Boilerplate.Infrastructure/Messaging" in f: return "events-inprocess.md"
    if "Boilerplate.Infrastructure/Logging" in f or "Tests/Infrastructure/Logging" in f: return "logging.md"
    if "Boilerplate.Infrastructure" in f: return "infrastructure-postgres.md"
    if "Boilerplate.Api" in f or "Boilerplate.Console" in f: return "hosts.md"
    if "Boilerplate.Domain" in f: return "domain.md" if not is_example(f) else "worked-slice.md"
    if "Boilerplate.Application" in f: return "application.md" if not is_example(f) else "worked-slice.md"
    return "solution.md"
def source_of(f):
    if f == "src/Boilerplate.Worker/Program.cs":
        return "data"
    return "snippets" if f.endswith(".cs") else "data"


out = []
for f in files:
    entry = {"path": f.replace("Boilerplate", "{Name}"), "page": KERNEL_PAGES.get(f, page_of(f)), "source": source_of(f)}
    ex = is_example(f)
    if ex:
        entry["when"] = {"example": "true"}
    entry["kernel"] = (not ex) and f.endswith(".cs") and not f.startswith("test/")
    if f.startswith(OUTBOX_PREFIXES):
        entry["when"] = {"events.outbox": OUTBOX}
        entry["kernel"] = False
        entry["page"] = "outbox.md"
    if f.startswith(RABBITMQ_PREFIXES):
        entry["when"] = {"events.transport": "rabbitmq"}
        entry["kernel"] = False
        entry["page"] = "rabbitmq.md"
    if f.startswith(EVENT_CONSUMER_FILES):
        entry["kernel"] = False
        entry["page"] = "event-consumers.md"
    if f in OPTION_FILES:
        entry["when"] = OPTION_FILES[f]
        entry["kernel"] = False
        entry["page"] = "event-consumers.md" if f.startswith(EVENT_CONSUMER_FILES) else OPTION_PAGES.get(f, "correlation.md" if "Correlation" in f else "outbox.md" if "Worker" in f else "events-inprocess.md" if ("EventPublisher" in f or "MessagingConstants" in f) else "failures.md")
    in_all = all(f in present[preset] for preset in PRESETS)
    assert in_all or "when" in entry and set(entry["when"]) != {"example"}, f"{f} is in only some variants and has no option"
    out.append(entry)
initial_when = [{"example": "true"}, {"events.outbox": OUTBOX}]
out.append({"path": "src/{Name}.Infrastructure/Persistence/Migrations/*_Initial.cs", "page": "infrastructure-postgres.md", "source": "tool", "when_any": initial_when, "kernel": False, "glob": True})
out.append({"path": "src/{Name}.Infrastructure/Persistence/Migrations/*_Initial.Designer.cs", "page": "infrastructure-postgres.md", "source": "tool", "when_any": initial_when, "kernel": False, "glob": True})
out.append({"path": "src/{Name}.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs", "page": "infrastructure-postgres.md", "source": "tool", "when_any": initial_when, "kernel": False})
out.append({"path": "MORTIS.md", "page": "project-docs.md", "source": "tool", "kernel": False})
out.sort(key=lambda e: e["path"])
manifest={
 "language":"csharp",
 "manifest_version":1,
 "placeholders":{"Name":"the `name` row of MORTIS.md","Aggregate":"an aggregate root's type name, singular PascalCase","Plural":"the aggregate's folder name, plural PascalCase"},
 "files":out,
 "aggregate_files":{
   "description":"Files a project adds per aggregate. Every file in these folders must match one pattern; verify.sh fails on any other file there.",
   "folders":{
     "src/{Name}.Domain/Aggregates/{Plural}":["{Aggregate}.cs","I{Aggregate}Repository.cs","{Aggregate}Constants.cs","{Aggregate}Status.cs","*Model.cs","Events/*.cs","*.cs"],
     "src/{Name}.Application/{Plural}":["{Aggregate}Dto.cs","*Command.cs","*When*EventHandler.cs"],
     "src/{Name}.Infrastructure/Persistence/{Plural}":["{Aggregate}Configuration.cs","{Aggregate}Repository.cs"],
     "src/{Name}.Api/Controllers":["{Plural}Controller.cs"],
     "test/{Name}.Tests/Domain/{Plural}":["*Tests.cs"],
     "test/{Name}.Tests/Application/{Plural}":["*CommandHandlerTests.cs","*EventHandlerTests.cs"],
     "test/{Name}.Tests/Infrastructure/Persistence/{Plural}":["{Aggregate}ConfigurationTests.cs","{Aggregate}RepositoryTests.cs"],
     "test/{Name}.Tests/TestSupport/Domain/{Plural}":["Fake*.cs","{Aggregate}RepositoryBuilder.cs"]
   }
 }
}
old = json.load(open(TARGET))
manifest["aggregate_files"] = old["aggregate_files"]
manifest["always_allowed"] = old["always_allowed"]
json.dump(manifest, open(TARGET, "w"), indent=1)
print(len(out), "entries")
