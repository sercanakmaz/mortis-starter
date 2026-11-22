"""Mechanical review of a Mortis C# project against the rules of its AGENTS.md.

usage: review.py <project-dir> [--diff <git-range>] [--format text|json] [--dry-run]

Each check has an id, a severity, the rule it enforces and a fix. Folder mode reviews every file; --diff reviews the files the
range changed (registration and contract checks still read the whole project). Exit code 1 when an error is found.
"""
import json
import re
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
SKIP_PARTS = {"bin", "obj", ".mortis", ".git", "Migrations", "TestResults", "node_modules"}

CHECKS = [
    ("C01", "error", "AGENTS 15", "comments in code", "Move the reason to docs/ or an ADR and delete the comment."),
    ("C02", "error", "AGENTS 8", "a private method or local helper in a command handler, event handler, event consumer or domain service", "Move the rule to the aggregate, a value object or a domain service; keep the steps inline."),
    ("C03", "warning", "AGENTS 1, 10", "a command handler that branches on aggregate state or throws a business rule (a rule outside the aggregate)", "Move the condition into an aggregate method that throws its coded exception."),
    ("C04", "error", "AGENTS 10", "a host (Api, Console, Worker) that names a handler type", "Inject ICommandDispatcher and Send the command."),
    ("C05", "error", "AGENTS 13", "a handler, event handler or event consumer that is not registered", "Add its AddCommandHandler / AddScoped<BaseSyncEventHandler<...>> line in ApplicationModule, or its AddEventConsumer line in the host."),
    ("C06", "error", "AGENTS 20", "a writing command (ITransactionalCommand) without IContextCommand", "Implement IContextCommand with the one [JsonIgnore] Context line."),
    ("C07", "error", "R13, tenancy.md", "a unique index or alternate key of a tenant-owned entity without TenantId", "Put TenantId first in the index or key."),
    ("C08", "error", "AGENTS 10", "an event handler or consumer that injects a repository or a domain service", "Send a command through ICommandDispatcher; the command's handler loads and saves."),
    ("C09", "error", "AGENTS 14", "a test class with a field, a constructor or a method that is not a test", "Move the helper, field or setup to TestSupport/ (a builder or a fake)."),
    ("C10", "error", "AGENTS 4", "a banned API (Guid.NewGuid, DateTime.Now, DateTime.Today, DateTimeOffset.Now, .Result, .Wait())", "Use Guid.CreateVersion7(), DateTime.UtcNow, and await."),
    ("C11", "error", "testing-levels.md", "a queue event changed while event-contracts.json did not", "Rewrite the golden file deliberately with MORTIS_UPDATE_CONTRACTS=1 and review the change."),
    ("C12", "error", "AGENTS 8, decision 31", "a write, save, transaction or IUnitOfWork in the Domain layer", "Move the write to the command handler; the domain decides, the handler persists."),
    ("C13", "warning", "decision 31", "a domain service that takes a whole repository", "Give it a narrow read port in Domain/Ports/ instead."),
    ("C14", "error", "contexts.md", "a context that refers to another context's types other than its events", "React to the other context's event, or pass an id."),
]
SEVERITY_ORDER = {"error": 0, "warning": 1}


def load_decisions(project):
    sys.path.insert(0, str(HERE.parent.parent / "mortis-scaffold" / "scripts"))
    try:
        from decisions import load
        errors, _, values = load(str(project / "MORTIS.md"))
        return {} if errors else values
    except Exception:
        return {}


def source_files(project):
    for path in sorted(project.rglob("*.cs")):
        relative = path.relative_to(project)
        if not (SKIP_PARTS & set(relative.parts)):
            yield relative.as_posix(), path.read_text(encoding="utf-8", errors="replace")


def changed_files(project, git_range):
    output = subprocess.run(["git", "-C", str(project), "diff", "--name-only", git_range], capture_output=True, text=True, check=True).stdout
    return {line.strip() for line in output.splitlines() if line.strip()}


def strip_strings(line):
    return re.sub(r'@?\$?"(?:[^"\\]|\\.)*"', '""', line)


def lines_of(text):
    return text.splitlines()


def check_comments(files):
    for path, text in files:
        in_verbatim = False
        for number, line in enumerate(lines_of(text), 1):
            if '"""' in line:
                in_verbatim = not in_verbatim if line.count('"""') % 2 else in_verbatim
                continue
            if in_verbatim:
                continue
            code = strip_strings(line).strip()
            if code.startswith("//") or code.startswith("/*") or re.search(r"[;{})]\s*//", code):
                yield path, number, "a comment"


ORCHESTRATION = re.compile(r"(CommandHandler|EventHandler|EventConsumer)\b")
PRIVATE_METHOD = re.compile(r"^\s*private\s+(?:static\s+)?(?:async\s+)?(?!readonly\b)(?!const\b)[\w<>\[\],?.() ]+?\s+\w+\s*(?:<[^>]*>)?\s*\(")


def class_blocks(text):
    """Yields (class name, first line, last line) for each class, by brace depth."""
    lines = lines_of(text)
    for match in re.finditer(r"^\s*(?:public|internal)?\s*(?:sealed\s+|abstract\s+|static\s+|partial\s+)*class\s+(\w+)", text, re.M):
        start = text[:match.start()].count("\n")
        depth, opened = 0, False
        for index in range(start, len(lines)):
            depth += lines[index].count("{") - lines[index].count("}")
            opened = opened or "{" in lines[index]
            if opened and depth <= 0:
                yield match.group(1), start + 1, index + 1
                break
        else:
            yield match.group(1), start + 1, len(lines)


def check_private_helpers(files):
    for path, text in files:
        domain_service = "/Domain/" in path and "/Services/" in path
        lines = lines_of(text)
        for name, first, last in class_blocks(text):
            if not (ORCHESTRATION.search(name) or (domain_service and name.endswith("Service"))):
                continue
            for number in range(first, last + 1):
                if PRIVATE_METHOD.search(lines[number - 1]):
                    yield path, number, f"a private method in {name}"


def check_rules_in_handlers(files):
    for path, text in files:
        if not re.match(r"src/[^/]+\.Application/", path):
            continue
        lines = lines_of(text)
        for name, first, last in class_blocks(text):
            if not name.endswith("CommandHandler"):
                continue
            for number in range(first, last + 1):
                line = strip_strings(lines[number - 1])
                if "throw new BusinessRuleException" in line or re.search(r"\bif\s*\(.*\.\s*(Status|Is[A-Z]\w*)\b", line):
                    yield path, number, f"{name} decides a business rule"


def check_hosts_name_handlers(files, name):
    hosts = tuple(f"src/{name}.{host}/" for host in ("Api", "Console", "Worker"))
    for path, text in files:
        if not path.startswith(hosts):
            continue
        for number, line in enumerate(lines_of(text), 1):
            found = re.search(r"\b(\w+(?:CommandHandler|EventHandler))\b", strip_strings(line))
            if found and not found.group(1).startswith("Base"):
                yield path, number, f"the host names {found.group(1)}"


def check_registrations(all_files, name):
    module = next((text for path, text in all_files if path.endswith(f"src/{name}.Application/ApplicationModule.cs")), "")
    hosts = "\n".join(text for path, text in all_files if re.match(rf"src/{re.escape(name)}\.(Api|Console|Worker)/", path) or path.endswith("Module.cs"))
    for path, text in all_files:
        if not path.startswith(f"src/{name}.Application/"):
            continue
        for match in re.finditer(r"^\s*public\s+(?:sealed\s+)?class\s+(\w+(?:CommandHandler|EventHandler|EventConsumer))\b", text, re.M):
            handler = match.group(1)
            number = text[:match.start()].count("\n") + 1
            if handler.endswith("EventConsumer"):
                if not re.search(rf"AddEventConsumer<\s*{handler}\s*>", hosts):
                    yield path, number, f"{handler} has no AddEventConsumer line in a host"
            elif not re.search(rf"\b{handler}\b", module):
                yield path, number, f"{handler} is not registered in ApplicationModule"


def check_context_on_writes(files):
    for path, text in files:
        if not path.startswith("src/"):
            continue
        for match in re.finditer(r"record\s+(\w+Command)\b([^{;]*)", text):
            interfaces = match.group(2)
            if "ITransactionalCommand" in interfaces and "IContextCommand" not in interfaces:
                yield path, text[:match.start()].count("\n") + 1, f"{match.group(1)} writes without IContextCommand"


def check_tenant_indexes(all_files, files, name):
    tenant_owned = set()
    for path, text in all_files:
        if path.startswith(f"src/{name}.Domain/"):
            for match in re.finditer(r"class\s+(\w+)\s*:[^{]*\bIMultiTenant\b", text):
                tenant_owned.add(match.group(1))
    for path, text in files:
        configured = re.search(r"IEntityTypeConfiguration<(\w+)>", text)
        if not configured or configured.group(1) not in tenant_owned:
            continue
        for match in re.finditer(r"(HasIndex\([^;]*?\.IsUnique\(\)|HasAlternateKey\([^;]*?\))", text, re.S):
            if "TenantId" not in match.group(1):
                yield path, text[:match.start()].count("\n") + 1, f"a unique key of tenant-owned {configured.group(1)} without TenantId"


def check_reactions_inject(files):
    for path, text in files:
        for match in re.finditer(r"class\s+(\w+(?:EventHandler|EventConsumer))\s*\(([^)]*)\)", text):
            for parameter in re.findall(r"\b(I\w+(?:Repository|Service))\b", match.group(2)):
                if parameter != "ICacheService":
                    yield path, text[:match.start()].count("\n") + 1, f"{match.group(1)} injects {parameter}"


TEST_ATTRIBUTE = re.compile(r"\[(Fact|Theory|SkippableFact|SkippableTheory|InlineData|MemberData|ClassData|Trait|Collection)\b")
MEMBER = re.compile(r"^\s{4}(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:readonly\s+)?[\w<>\[\],?.() ]+?\s+(\w+)\s*(\(|=|;|\{)")


def check_test_classes(files):
    for path, text in files:
        if not path.startswith("test/") or "/TestSupport/" in path or not path.endswith("Tests.cs"):
            continue
        lines = lines_of(text)
        for number, line in enumerate(lines, 1):
            found = MEMBER.match(line)
            if not found:
                continue
            previous = "\n".join(lines[max(0, number - 12):number - 1])
            last_member = max((i for i in range(number - 1) if MEMBER.match(lines[i]) or lines[i].strip() == "}"), default=-1)
            attributes = "\n".join(lines[last_member + 1:number - 1]) if last_member >= 0 else previous
            if found.group(2) == "(" and TEST_ATTRIBUTE.search(attributes):
                continue
            yield path, number, f"{found.group(1)} is not a test"


BANNED = [(re.compile(r"\bGuid\.NewGuid\s*\("), "Guid.NewGuid()"), (re.compile(r"\bDateTime\.Now\b"), "DateTime.Now"), (re.compile(r"\bDateTime\.Today\b"), "DateTime.Today"),
          (re.compile(r"\bDateTimeOffset\.Now\b"), "DateTimeOffset.Now"), (re.compile(r"\)\.Result\b|Async\([^)]*\)\.Result\b|\bTask\w*\.Result\b"), ".Result"), (re.compile(r"\.Wait\(\s*\)"), ".Wait()")]


def check_banned(files):
    for path, text in files:
        for number, line in enumerate(lines_of(text), 1):
            code = strip_strings(line)
            for pattern, label in BANNED:
                if pattern.search(code):
                    yield path, number, f"{label}"


def check_contracts(all_files, changed, project):
    golden = next(project.rglob("event-contracts.json"), None)
    if golden is None:
        return
    golden_path = golden.relative_to(project).as_posix()
    if golden_path in changed:
        return
    for path, text in all_files:
        if path in changed and re.search(r"\bIQueueEvent\b", text) and re.search(r"record\s+\w+\(", text):
            yield path, 1, "a queue event changed without its golden file"


DOMAIN_WRITES = re.compile(r"\.(CreateAsync|UpdateAsync|DeleteAsync|UpsertAsync|SaveChangesAsync|SaveChanges|BeginTransactionAsync|CommitTransactionAsync|RollbackTransactionAsync)\s*\(|\bIUnitOfWork\b")


def check_domain_writes(files, name):
    for path, text in files:
        if not path.startswith(f"src/{name}.Domain/"):
            continue
        for number, line in enumerate(lines_of(text), 1):
            found = DOMAIN_WRITES.search(strip_strings(line))
            if found:
                yield path, number, f"{found.group(0).strip('.(')} in the domain"


def check_service_repositories(files, name):
    for path, text in files:
        if not path.startswith(f"src/{name}.Domain/") or "/Services/" not in path:
            continue
        for match in re.finditer(r"class\s+(\w+Service)\s*\(([^)]*)\)", text):
            for parameter in re.findall(r"\b(I\w+Repository)\b", match.group(2)):
                yield path, text[:match.start()].count("\n") + 1, f"{match.group(1)} takes {parameter}"


def check_contexts(files, name):
    for path, text in files:
        own = re.search(r"/Contexts/(\w+)/", path)
        if not own:
            continue
        for number, line in enumerate(lines_of(text), 1):
            for other, rest in re.findall(rf"\b{re.escape(name)}\.(?:Domain|Application)\.Contexts\.(\w+)((?:\.\w+)*)", line):
                if other != own.group(1) and not rest.endswith(".Events"):
                    yield path, number, f"refers to context {other} ({rest.lstrip('.') or 'its root'})"


def review(project, git_range=None):
    values = load_decisions(project)
    name = values.get("name") or next((p.stem for p in project.glob("*.slnx")), project.name)
    all_files = list(source_files(project))
    changed = changed_files(project, git_range) if git_range else None
    files = [f for f in all_files if changed is None or f[0] in changed]
    addons = values.get("addons", []) or []
    findings, not_run = [], []

    def add(check_id, results):
        check = next(c for c in CHECKS if c[0] == check_id)
        for path, line, message in results:
            findings.append({"check": check_id, "severity": check[1], "rule": check[2], "file": path, "line": line, "message": message, "fix": check[4]})

    add("C01", check_comments(files))
    add("C02", check_private_helpers(files))
    add("C03", check_rules_in_handlers(files))
    add("C04", check_hosts_name_handlers(files, name))
    add("C05", check_registrations(all_files, name))
    add("C06", check_context_on_writes(files))
    if values.get("tenancy.mode") == "interface":
        add("C07", check_tenant_indexes(all_files, files, name))
    else:
        not_run.append(("C07", "tenancy.mode is not interface"))
    add("C08", check_reactions_inject(files))
    add("C09", check_test_classes(files))
    add("C10", check_banned(files))
    if changed is not None:
        add("C11", check_contracts(all_files, changed, project))
    else:
        not_run.append(("C11", "needs --diff <git-range>"))
    add("C12", check_domain_writes(files, name))
    add("C13", check_service_repositories(files, name))
    if "contexts" in addons:
        add("C14", check_contexts(files, name))
    else:
        not_run.append(("C14", "the contexts add-on is off"))
    findings.sort(key=lambda f: (SEVERITY_ORDER[f["severity"]], f["check"], f["file"], f["line"]))
    return findings, not_run


def main(argv):
    if len(argv) < 2:
        print(__doc__, file=sys.stderr)
        return 2
    project = Path(argv[1]).resolve()
    if "--dry-run" in argv:
        values = load_decisions(project)
        for check_id, severity, rule, description, _ in CHECKS:
            applies = not ((check_id == "C07" and values.get("tenancy.mode") != "interface") or (check_id == "C14" and "contexts" not in (values.get("addons") or [])) or check_id == "C11" and "--diff" not in argv)
            print(f"PLAN  {check_id} {severity:<7} {rule:<22} {description}{'' if applies else '  (not run here)'}")
        return 0
    git_range = argv[argv.index("--diff") + 1] if "--diff" in argv else None
    output_format = argv[argv.index("--format") + 1] if "--format" in argv else "text"
    findings, not_run = review(project, git_range)
    if output_format == "json":
        print(json.dumps({"findings": findings, "not_run": [{"check": c, "why": w} for c, w in not_run]}, indent=1))
    else:
        for finding in findings:
            print(f"{finding['severity'].upper():<7} {finding['check']} {finding['file']}:{finding['line']}  {finding['message']}  [{finding['rule']}]  Fix: {finding['fix']}")
        for check_id, why in not_run:
            print(f"NOT RUN {check_id}: {why}")
        errors = sum(1 for f in findings if f["severity"] == "error")
        print(f"{errors} error(s), {len(findings) - errors} warning(s)")
    return 1 if any(f["severity"] == "error" for f in findings) else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
