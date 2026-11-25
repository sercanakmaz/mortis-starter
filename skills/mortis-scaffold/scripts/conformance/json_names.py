"""Renames the keys of a JSON body between the checks' camelCase and the project's naming.json.
json_names.py out <naming> '<json>' prints the body to send; json_names.py in <naming> <file> rewrites a response file in place.
Only keys are renamed; a dictionary of field errors keeps its keys, which name the command's members."""
import json
import re
import sys

direction, naming, argument = sys.argv[1:4]


def to_snake(key):
    return re.sub(r"(?<=[a-z0-9])([A-Z])", r"_\1", key).lower()


def to_camel(key):
    return re.sub(r"_([a-z0-9])", lambda m: m.group(1).upper(), key)


def rename(value, change, under_errors=False):
    if isinstance(value, dict):
        return {(key if under_errors else change(key)): rename(item, change, key in ("errors",)) for key, item in value.items()}
    if isinstance(value, list):
        return [rename(item, change) for item in value]
    return value


if naming != "snake_case":
    if direction == "out":
        print(argument)
    sys.exit(0)
if direction == "out":
    print(json.dumps(rename(json.loads(argument), to_snake)) if argument else "")
else:
    try:
        body = json.load(open(argument))
    except (OSError, ValueError):
        sys.exit(0)
    json.dump(rename(body, to_camel), open(argument, "w"))
