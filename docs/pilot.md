# Pilot generation (phase 1, after step 03)

Asked for by the maintainer: one generation of the lean preset, on one model, to see whether a project can be rebuilt from snippets before more options are written on the same foundations.

## The run

| | |
|---|---|
| Date | 2026-11-17 |
| Request | "lean, C#, PostgreSQL, called Acme" (lean includes the example) |
| Model | mid-size (Sonnet class), unattended, reading only the two skill folders |
| Time | 18 minutes, about 320,000 tokens, 49 tool calls |
| Build-fix cycles | 1 (every step's build green first time; one fix for the order of `ICommand.cs`) |
| Tests | 129 unit tests green |
| `verify.sh` | 11 passed, 1 failed, 0 skipped; all conformance (database, Console, HTTP) passed against PostgreSQL |
| The failure | a skill defect: `kernel-shape.txt` held the template's database name in `DesignTimeConnectionString`. Fixed (`{database}` placeholder); the pilot project then matches the shape and the structure check |

**Answer to the pilot's question:** yes. A mid-size model rebuilt the lean project with the example from member snippets, and it behaved exactly as the hand-written solution did. Whether a small model can do the same is phase 2, step 05.

## What the run found, and what was done

| Finding | Done |
|---|---|
| Steps 3, 6 and 8 of the procedure each needed types of a later step | Procedure reordered (failures and contracts before the pipeline; dispatchers before persistence); every registration file is written with an empty body first |
| The worked slice's order (Product, Customer, Order) was impossible: product and customer handlers react to order events | Order changed: all domain first, then application Order, Customer, Product, then infrastructure, then controllers |
| No usings or namespaces were listed | Every page now ends with a generated table: file, namespace, usings |
| `ICommand.cs` order contradicted the shape file | Order stated |
| Project-file blocks unlabelled | Each block labelled with its path |
| Two controllers had no `[Route]` snippet | Declarations now include their attributes |
| `ScenarioConstants` snippets lacked `;` | Extraction fixed |
| The positional command with a body had no snippet | Added to `application.md` |
| The `DomainModule` snippet showed an illustration | The snippet is the empty body; the illustration is prose |
| ADR dates 0001 to 0005 were fixed dates | All dates are the generation day |
| `.mortis/` not mentioned as exempt | Stated in `SKILL.md` |
| About 25 handler and event-handler test bodies are names only; the model invented them | **Open.** Write the bodies before phase 2 step 05, or measure first whether a small model manages from the two shapes |
| A long scenario method is split across two code blocks | Kept: the sixty-line rule; the text says the member continues |

The model's full notes were kept for this write-up and are not stored.
