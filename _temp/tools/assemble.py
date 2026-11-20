import importlib.util
import shutil
import subprocess
import sys
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
TEMPLATE = TOOLS.parent / "template" / "assets" / "csharp"
OVERLAY = TOOLS / "overlay"
VARIANTS = TOOLS / "overlay-variants"
OUTBOX_PARTS = ("src/Boilerplate.Infrastructure/Messaging/Outbox/", "test/Boilerplate.Tests/Infrastructure/Messaging/Outbox/", "src/Boilerplate.Worker/")
NONE_PARTS = ("src/Boilerplate.Infrastructure/Messaging/EventPublisher.cs", "src/Boilerplate.Infrastructure/Messaging/MessagingConstants.cs",
              "test/Boilerplate.Tests/Infrastructure/Messaging/EventPublisherTests.cs", "test/Boilerplate.Tests/TestSupport/Infrastructure/Messaging/EventPublisherBuilder.cs")


def main(mode, target):
    target = Path(target)
    if target.exists():
        shutil.rmtree(target)
    shutil.copytree(TEMPLATE, target, ignore=shutil.ignore_patterns("bin", "obj"))
    spec = importlib.util.spec_from_file_location("expand_pages", TOOLS / "expand_pages.py")
    expand_pages = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(expand_pages)
    for path in target.rglob("*.cs"):
        text = path.read_text()
        changed = text
        for old, new in expand_pages.REPLACEMENTS:
            changed = changed.replace(old, new)
        if changed != text:
            path.write_text(changed)
    shutil.copytree(OVERLAY, target, dirs_exist_ok=True)
    for relative in (target / "REMOVED.txt").read_text().split():
        (target / relative).unlink(missing_ok=True)
    (target / "REMOVED.txt").unlink()
    if mode == "none":
        for part in OUTBOX_PARTS:
            path = target / part
            shutil.rmtree(path) if path.is_dir() else path.unlink(missing_ok=True)
    else:
        for part in NONE_PARTS:
            (target / part).unlink(missing_ok=True)
        for variant in ("outbox", mode):
            if (VARIANTS / variant).exists():
                shutil.copytree(VARIANTS / variant, target, dirs_exist_ok=True)
        program = target / "src/Boilerplate.Api/Program.cs"
        program.write_text(program.read_text()
                           .replace("    .AddInfrastructureModule(builder.Configuration);", "    .AddInfrastructureModule(builder.Configuration)\n    .AddOutboxWorker();")
                           .replace("using Boilerplate.Infrastructure;\n", "using Boilerplate.Infrastructure.Messaging.Outbox;\nusing Boilerplate.Infrastructure;\n", 1))
        subprocess.run(["dotnet", "sln", str(target / "Boilerplate.slnx"), "add", str(target / "src/Boilerplate.Worker/Boilerplate.Worker.csproj")], check=True, capture_output=True)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
