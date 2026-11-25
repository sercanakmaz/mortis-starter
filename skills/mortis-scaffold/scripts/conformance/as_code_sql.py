"""Rewrites a check's SQL, written with snake_case names, for a database whose names are as in the code (naming.database = as-code).
A bare snake_case name that is the snake form of a real table or column becomes that name, quoted; inside a string, only a name
compared with table_name, column_name or relname is rewritten (unquoted). Usage: as_code_sql.py "<sql>" < names (one real name per line)."""
import re
import sys


def snake(name):
    return re.sub(r"(?<=[a-z0-9])([A-Z])|(?<=[A-Z])([A-Z][a-z])", lambda m: "_" + (m.group(1) or m.group(2)), name).lower()


names = {snake(line.strip()): line.strip() for line in sys.stdin if line.strip()}
names = {key: value for key, value in names.items() if key != value}
query = sys.argv[1]
out, position = [], 0
for literal in re.finditer(r"'(?:[^']|'')*'", query):
    out.append(re.sub(r"\b[a-z][a-z0-9_]*\b", lambda m: f'"{names[m.group(0)]}"' if m.group(0) in names else m.group(0), query[position:literal.start()]))
    text = literal.group(0)[1:-1]
    before = query[:literal.start()]
    compared = re.search(r"\b(table_name|column_name|relname)\s*(=|<>|in\s*\((\s*'[^']*'\s*,)*\s*)\s*$", before)
    out.append("'" + (names.get(text, text) if compared else text) + "'")
    position = literal.end()
out.append(re.sub(r"\b[a-z][a-z0-9_]*\b", lambda m: f'"{names[m.group(0)]}"' if m.group(0) in names else m.group(0), query[position:]))
print("".join(out))
