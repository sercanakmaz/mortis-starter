# reference

The reference builds the skills are written from. Nothing here is installed with the skills (`scripts/install.sh` copies only `skills/*`), and no skill page points here: a model using the skills never sees this folder. It stays in the repository because every code snippet on a skill page is expanded from a file here, and `tests/check_docs.py` fails when a page and its source differ (decisions 58 and 60).

| Path | What it is |
|---|---|
| `variants/lean/`, `variants/standard/`, `variants/advanced/` | **the source of truth for C#**: one complete solution per preset (named `Boilerplate`, with the example), each with its own `MORTIS.md`. Each builds, tests and passes `skills/mortis-scaffold/scripts/verify.sh`. A change is made in every variant it concerns, by hand; `tests/check_variants.py` fails on a difference no option explains |
| `variants-extra/` | option builds: `standard-a` (MongoDB), `lean-b` (`int` keys, as-code names, snake_case JSON, prefixed events), `advanced-t` (text tenant ids), `standard-w` (a separate Worker with the background outbox) |
| `variants-go/lean/` | the source of truth for Go (lean preset) |
| `interop/` | the interoperability tests of the RabbitMQ bus and the outbox against MassTransit 8, C# and Go (`tests/check_interop*.sh`) |
| `tools/pages/`, `tools/docs-src/`, `tools-go/pages/` | the page sources and the project-document sources |
| `tools/expand_pages.py`, `tools/build_manifest.py`, `tools/build_shape.py`, `tools/build_project_docs.py`, `tools-go/build_go.py` | write the pages, the manifest, the kernel shape and the project-document pages from the builds |
| `tools/csharp-example.txt`, `tools/shape-*.txt` | the list of files that make up the example; the kernel-shape fragments `build_shape.py` appends |

After a change to a build, rerun the C# tools, then the checks:

```bash
python3 reference/tools/build_manifest.py && python3 reference/tools/expand_pages.py && python3 reference/tools/build_shape.py && python3 reference/tools/build_project_docs.py
python3 -I tests/check_docs.py && python3 -I tests/check_variants.py
```

A page marker reads the first variant of lean, advanced, standard that has the file; `variant:<preset>/<file>` reads one variant, `extra:<build>/<file>` one option build.
