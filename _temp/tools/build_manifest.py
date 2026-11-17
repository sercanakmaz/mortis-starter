import json, os, re
from pathlib import Path
T = Path("/home/claude/zeyrek-skills/_temp/template/assets/csharp")
files = sorted(str(p.relative_to(T)) for p in T.rglob("*") if p.is_file() and not ({"bin","obj"} & set(p.parts)))
deletes = [l.split(" ",1)[1] for l in (T.parent/"csharp-example.txt").read_text().splitlines() if l.startswith("delete ")]
def is_example(f):
    return any(f == d or f.startswith(d + "/") for d in deletes)
superseded = re.compile(r"(Domain/Aggregates/\w+/\w+(Service|Module)\.cs|TestSupport/Domain/\w+/\w+ServiceBuilder\.cs|Tests/Domain/\w+/\w+ServiceTests\.cs)$")
def page_of(f):
    if f in ("BannedSymbols.txt","Directory.Build.props","Directory.Packages.props","global.json",".editorconfig",".gitignore","Boilerplate.slnx","Dockerfile","docker-compose.yml") or f.endswith(".csproj"): return "solution.md"
    if f.startswith(".vscode/"): return "hosts.md"
    if f.startswith("docs/adr/"): return "project-docs-adr.md"
    if f == "docs/architecture.md": return "project-docs-architecture.md"
    if f.endswith(".md"): return "project-docs.md"
    if "/Migrations/" in f: return "infrastructure-postgres.md"
    if f.startswith("test/"):
        if is_example(f): return "worked-slice-tests.md"
        return "testing.md" if "/TestSupport/" in f else "testing-kernel.md"
    if "Boilerplate.Shared/Domain" in f: return "shared-domain.md"
    if "Boilerplate.Shared/Application" in f: return "shared-application.md"
    if "Boilerplate.Shared/" in f: return "shared-other.md"
    if "Boilerplate.Infrastructure/Messaging" in f: return "events-inprocess.md"
    if "Boilerplate.Infrastructure" in f: return "infrastructure-postgres.md"
    if "Boilerplate.Api" in f or "Boilerplate.Console" in f: return "hosts.md"
    if "Boilerplate.Domain" in f: return "domain.md" if not is_example(f) else "worked-slice.md"
    if "Boilerplate.Application" in f: return "application.md" if not is_example(f) else "worked-slice.md"
    return "solution.md"
def source_of(f):
    if "/Migrations/" in f: return "tool"
    if f.endswith((".cs",)): return "snippets"
    return "data"
out=[]
dropped=[]
for f in files:
    if superseded.search(f):
        dropped.append(f); continue
    if "/Migrations/" in f:
        continue
    ex = is_example(f) or f in ("src/Boilerplate.Console/Scenarios/BlockedCustomerScenario.cs","src/Boilerplate.Console/Scenarios/OrderFlowScenario.cs","src/Boilerplate.Console/Scenarios/StockFlowScenario.cs")
    entry = {"path": f.replace("Boilerplate", "{Name}"), "page": page_of(f), "source": source_of(f)}
    if ex: entry["when"] = {"example": "true"}
    entry["kernel"] = (not ex) and f.endswith(".cs") and not f.startswith("test/")
    out.append(entry)
# decision 28 additions: handler tests replacing service tests
for cmd,plural in [("CreateCustomer","Customers"),("BlockCustomer","Customers"),("RegisterCompletedOrder","Customers"),("AddOrderLine","Orders"),("CancelOrderLine","Orders"),("CompleteOrder","Orders"),("CancelOrderWhenAllLinesCancelled","Orders")]:
    p=f"test/{{Name}}.Tests/Application/{plural}/{cmd}CommandHandlerTests.cs"
    if not any(e["path"]==p for e in out):
        out.append({"path":p,"page":"worked-slice-tests.md","source":"snippets","when":{"example":"true"},"kernel":False})
out.append({"path":"src/{Name}.Infrastructure/Persistence/Migrations/*_Initial.cs","page":"infrastructure-postgres.md","source":"tool","when":{"example":"true"},"kernel":False,"glob":True})
out.append({"path":"src/{Name}.Infrastructure/Persistence/Migrations/*_Initial.Designer.cs","page":"infrastructure-postgres.md","source":"tool","when":{"example":"true"},"kernel":False,"glob":True})
out.append({"path":"src/{Name}.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs","page":"infrastructure-postgres.md","source":"tool","when":{"example":"true"},"kernel":False})
out.append({"path":"docs/adr/0006-command-handlers-are-the-use-cases.md","page":"project-docs-adr.md","source":"data","kernel":False})
out.append({"path":"MORTIS.md","page":"project-docs.md","source":"tool","kernel":False})
out.sort(key=lambda e:e["path"])
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
json.dump(manifest,open("/home/claude/zeyrek-skills/skills/mortis-scaffold/references/csharp/manifest.json","w"),indent=1)
print(len(out),"entries; dropped by decision 28:",dropped)
