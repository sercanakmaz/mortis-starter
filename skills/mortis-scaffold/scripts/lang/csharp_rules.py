import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from csharp_shape import mask, shape_of_file  # noqa: E402

SKIP_PARTS = {"bin", "obj", "Migrations"}
ORCHESTRATION = re.compile(r"(CommandHandler|EventHandler)$")
BANNED_TEXT = [
    (re.compile(r"\bFluentAssertions\b|\bShouldly\b|\bNUnit\b"), "assertions use xUnit Assert only"),
    (re.compile(r"\bMediatR\b|\bAutoMapper\b|\bMapster\b"), "no mediator or mapping library"),
    (re.compile(r"\.Scan\(|AddClasses\(|RegisterAssemblyTypes|ApplyConfigurationsFromAssembly\((?!typeof\(AppDbContext\))"), "registration is explicit"),
]


def source_files(root):
    for path in sorted(Path(root).rglob("*.cs")):
        if not (SKIP_PARTS & set(path.relative_to(root).parts)):
            yield path


def comment_lines(text):
    masked = mask(text)
    found = []
    for number, (original, line) in enumerate(zip(text.splitlines(), masked.splitlines()), 1):
        if original.strip() and not line.strip():
            found.append(number)
    return found


def public_types(path):
    names = []
    for line in shape_of_file(path):
        if line.startswith("type "):
            match = re.search(r"\b(?:class|record|interface|struct|enum)\s+(\w+)", line)
            if match:
                names.append(match.group(1))
    return names


def private_methods(text):
    masked = mask(text)
    return [m.group(1) for m in re.finditer(r"^\s*private\s+(?:static\s+|async\s+)*[\w<>\[\],\s]+?\s(\w+)\s*\(", masked, re.M)]


def test_class_members(text):
    masked = mask(text)
    problems = []
    for match in re.finditer(r"^(\s*)(public|private|protected|internal)\s+(?!class\b)(.*)$", masked, re.M):
        indent = len(match.group(1))
        if indent != 4:
            continue
        before = masked[:match.start()].rstrip().splitlines()
        previous = before[-1].strip() if before else ""
        if previous not in ("[Fact]",) and not previous.startswith(("[Theory]", "[InlineData", "[MemberData")):
            problems.append(match.group(3).strip()[:60])
    return problems


def check(root):
    root = Path(root)
    problems = []
    for path in source_files(root):
        relative = path.relative_to(root).as_posix()
        text = path.read_text(encoding="utf-8")
        for number in comment_lines(text):
            problems.append(f"{relative}:{number}: a comment; the code has no comments")
        for pattern, message in BANNED_TEXT:
            if pattern.search(mask(text)):
                problems.append(f"{relative}: {message}")
        if path.name != "Program.cs":
            names = public_types(path)
            if names and path.stem not in names:
                problems.append(f"{relative}: the file is named {path.stem} but holds no type of that name ({', '.join(names)})")
        is_test = relative.startswith("test/") and "/TestSupport/" not in relative
        if is_test and path.stem.endswith("Tests"):
            for member in test_class_members(text):
                problems.append(f"{relative}: a test class holds only [Fact] and [Theory] methods, found: {member}")
        orchestration = ORCHESTRATION.search(path.stem) or path.stem.endswith("Command") or "/Domain/Services/" in relative
        if orchestration and not is_test:
            for name in private_methods(text):
                problems.append(f"{relative}: private method {name} in a handler or domain service")
        if re.search(r"^src/[^/]+\.Application/.+Command\.cs$", relative):
            masked_text = mask(text)
            if len(re.findall(r"\[JsonIgnore\]", masked_text)) > 1 or re.search(r"\b(UserId|UserEmail|CorrelationId|TenantId)\s*\{\s*get", masked_text):
                problems.append(f"{relative}: a command declares its context in one line, Context, and nothing else")
        if relative.startswith("src/") and re.search(r'new BusinessRuleException\(\s*"', text):
            problems.append(f"{relative}: a rule code is a constant of the aggregate's Constants, never a string at the throw")
        if re.search(r"^src/[^/]+\.Domain/", relative):
            masked_text = mask(text)
            if re.search(r"\.(CreateAsync|UpdateAsync|DeleteAsync|UpsertAsync|SaveChangesAsync|CommitTransactionAsync|BeginTransactionAsync)\(|\bIUnitOfWork\b", masked_text):
                problems.append(f"{relative}: the domain never writes: no repository write, save, transaction or IUnitOfWork; the command handler persists")
        if re.search(r"^src/[^/]+\.Domain/Aggregates/\w+/\w+Service\.cs$", relative):
            problems.append(f"{relative}: no service per aggregate; the command handlers are the use cases")
    return problems


def main(argv):
    problems = check(argv[1])
    for problem in problems:
        print(problem)
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
