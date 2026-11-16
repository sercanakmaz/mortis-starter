# _temp (temporary)

Holds the C# solution that used to ship inside the `mortis-scaffold` skill, with the script that copied it. It is here so the skill itself contains no project source.

| Path | What it is |
|---|---|
| `template/assets/csharp/` | the complete C# solution (named `Boilerplate`), with the example |
| `template/assets/csharp-example.txt` | the list of files that make up the example |
| `template/scripts/scaffold.sh` | the old copy-and-rename script |

It is **source material** for phase 1: the kernel pages are written from this working code. It is not changed, not installed, and it is deleted when phase 2 has replaced it with projects the skills generated. `tests/smoke.sh` still builds and tests it from here.
