import importlib.util
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("decisions", HERE / "decisions.py")
decisions = importlib.util.module_from_spec(spec)
spec.loader.exec_module(decisions)


def main(argv):
    if len(argv) != 2:
        print("usage: validate_decisions.py MORTIS.md", file=sys.stderr)
        return 2
    path = Path(argv[1])
    if not path.exists():
        print(f"{path}: no such file", file=sys.stderr)
        return 2
    errors, notes, values = decisions.load(path)
    for error in errors:
        where = f"line {error['line']}" if error["line"] else "file"
        rule = f" [{error['rule']}]" if error["rule"] else ""
        print(f"{path}: {where}, {error['key']}{rule}: {error['message']}. Fix: {error['fix']}")
    for note in notes:
        print(f"{path}: note {note}")
    if errors:
        print(f"{len(errors)} problem(s)")
        return 1
    print("MORTIS.md is valid")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
