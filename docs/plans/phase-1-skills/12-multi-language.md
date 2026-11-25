# Phase 1, step 12 — Multi-language (knowledge only)

Needs: phase 1 steps 04, 07. Size: medium. Modules `language-messages`, `language-data` (`languages.mode`: `messages` includes only the first, `data` both). No C# is written, built or generated in this step.

## Goal

Two separate things people mean by "multi-language": messages the system produces in the caller's language, and business data stored in several languages.

## Design

**The language of a call.** The host puts it on `CommandContext.Language`: the Api from `Accept-Language` limited to `languages.supported` with `languages.default` as fallback; the Console from `--language`. Events carry it, so a notification sent by an event consumer is in the language of the person who caused it.

**Messages (`language-messages`).**
- An exception carries a code and parameters (needs `exceptions` = `coded` or `catalogued`). The message text is looked up at the edge: `IMessageLocalizer.Get(code, parameters, language)`.
- Texts live in one resource file per language (`Resources/messages.<lang>.json`), keyed by error code. A validator rule names a code, not a sentence.
- A test fails when a code has no text in the default language, and lists codes missing in the others.

**Data (`language-data`).**
- A value object `LocalizedText`: a map from language to text with `Get(language, fallback)`. Stored as one JSON column in PostgreSQL (a sub-document in MongoDB).
- An aggregate declares a translated property with that type (`Product.Name: LocalizedText`). A create or update command accepts either one text (stored under the call's language) or the full map.
- A DTO returns the text in the call's language and, on request, the full map.
- Searching and sorting by a translated field uses the JSON path for the language; one index per supported language is described, not generated.
- In `example-domain.md` (marked "when translated data is on"): `Product.Name`.

## Deliverables

Knowledge only. Nothing here is compiled or run in this phase.

1. **Layer 1** (`skills/mortis-architect/references/architecture/` and `references/options/`): what the option is, its pseudo-code, its rules, when to choose it and what it costs.
2. **Layer 2 for C#** (`skills/mortis-scaffold/references/csharp/`): one page per type or concern, as contract, behaviour rules, a snippet for every member with a real body, and the tests to write with their snippets. Snippets, never whole files.
3. **Entries** in `manifest.json` (every file the option adds, and which existing files it changes), `kernel-shape.txt` and `naming.md`.
4. **Conformance checks** (`skills/mortis-scaffold/scripts/conformance/`): one check per item under "Behaviour to prove in phase 2", selected by `MORTIS.md`. Written now, first run in phase 2.
5. **The decision record** a generated project gets for this option, as an outline in `references/csharp/project-docs.md`.
6. **A line in `docs/unverified.md`** for every page and check of this step: none of it has been compiled or run yet. Phase 2 removes the lines as it proves them.
7. `tests/check_docs.py` and `tests/check_installable.sh` pass.

## Behaviour to prove in phase 2

Each item becomes a conformance check or a test snippet now. None is run in this phase; phase 2 runs them on the generated project.

- [ ] The same failing request returns its message in `tr` with `Accept-Language: tr` and in the default language with an unsupported one (conformance).
- [ ] Removing a text from the default resource file fails a unit test.
- [ ] A product created with a Turkish name and updated with an English one returns each under its language and falls back to the default for a third (tests and conformance).
- [ ] An event consumer sees the language of the original request.

## Out of scope

Translating the example's texts beyond two languages; pluralization rules; right-to-left concerns; a translation-management screen or import/export.

## Notes (done)

Built and verified in the advanced variant, not knowledge only (decision 37); scope by the maintainer's answers (decision 44).

- The language comes from the token's `locale` claim, then `Accept-Language`, in the Api; from the token's claim in the Console (the plan's `--language` option was not built). Supported languages are generated constants.
- Messages: `ExceptionBase.Parameters`, `MessageLocalizer` (no interface: one implementation), `messages.en.json` and `messages.tr.json`; `ErrorCatalogue` keeps only `Codes`. Found on the way: MSBuild reads `messages.en.json` as an English culture resource and moves it into a satellite assembly unless `WithCulture="false"`; and a folder named `Languages` hides the `Languages` class, so the folder is `Localization`.
- Data: `LocalizedText` in jsonb through `LocalizedTextConversion`; `Product.Rename`, `RenameProductCommand`, `PUT /products/{id}/name`; `ProductDto.Names`. `GetProductCommand` became a context command so the host can give it a language.
- `LogFields` writes `Language` on every command and event line, in every variant.
- `build_shape.py` writes `languages.mode=messages|data` for the kernel lines the messages module changes; `failures.md` keeps the catalogue with messages as a hand-written snippet, since no variant has it any more.
- Proven: `languages.sh` (6) against the running hosts and the worker; lean 13, standard 15, advanced 18 checks of `verify.sh`.

