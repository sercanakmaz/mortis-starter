# _temp (temporary)

Holds the C# solution that used to ship inside the `mortis-scaffold` skill, with the script that copied it. It is here so the skill itself contains no project source.

| Path | What it is |
|---|---|
| `template/assets/csharp/` | the complete C# solution (named `Boilerplate`), with the example |
| `template/assets/csharp-example.txt` | the list of files that make up the example |
| `template/scripts/scaffold.sh` | the old copy-and-rename script |
| `pilot-acme/` | the project the pilot generated from the skills (lean, with the example), build output removed; the model's notes are in `.mortis/pilot-notes.md`. See `docs/pilot.md`. Generated before the pilot's fixes to the pages |
| `tools/expand_pages.py`, `tools/pages/`, `tools/docs-src/`, `tools/build_manifest.py` | the page sources and the script that writes the C# pages with member snippets extracted from the template; rerun after changing a page source: `python3 _temp/tools/expand_pages.py` |

It is **source material** for phase 1: the kernel pages are written from this working code. It is not changed, not installed, and it is deleted when phase 2 has replaced it with projects the skills generated. `tests/smoke.sh` still builds and tests it from here.
