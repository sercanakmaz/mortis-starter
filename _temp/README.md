# _temp (temporary)

Source material for phase 1. Nothing here is installed with the skills; it is deleted when phase 2 has replaced it with projects the skills generated.

| Path | What it is |
|---|---|
| `template/assets/csharp/` | the original hand-written C# solution (named `Boilerplate`), with the example. Historical: never changed, no longer read by any tool |
| `template/assets/csharp-example.txt` | the list of files that make up the example; `build_manifest.py` still reads it to tell example files from kernel files |
| `template/scripts/scaffold.sh` | the old copy-and-rename script |
| `variants/lean/`, `variants/standard/`, `variants/advanced/` | **the source of truth**: one complete solution per preset (named `Boilerplate`, with the example), each with its own `MORTIS.md`. Each builds, tests and passes `skills/mortis-scaffold/scripts/verify.sh`. A change is made in every variant it concerns, by hand; `tests/check_variants.py` fails on a difference no option explains |
| `interop/` | the MassTransit 8 interoperability tests of the RabbitMQ bus (`tests/check_interop.sh`) |
| `tools/pages/`, `tools/docs-src/` | the page sources and the project-document sources |
| `tools/expand_pages.py`, `tools/build_manifest.py`, `tools/build_shape.py`, `tools/build_project_docs.py` | write the C# pages, the manifest, the kernel shape and the project-document pages from the variants; rerun all four after a change: `python3 _temp/tools/build_manifest.py && python3 _temp/tools/expand_pages.py && python3 _temp/tools/build_shape.py && python3 _temp/tools/build_project_docs.py` |

A page marker reads the first variant of lean, advanced, standard that has the file; `variant:<preset>/<file>` reads one variant (for code that differs by option).

The variants hold only options built so far; the options of later steps join them as each step lands.
