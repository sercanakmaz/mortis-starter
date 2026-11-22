# Add a translated field

A field whose value has one text per language, shown in the caller's language. Needs `languages.mode` = `data`.

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | [languages.mode=data] the aggregate's test: setting one language keeps the others | `languages.md` | AGENTS 1 | red, then green |
| 2 | [languages.mode=data] the member is `LocalizedText`; the operation takes a language and a text | `languages.md` | AGENTS 1 | step 1 green |
| 3 | [languages.mode=data] `{Aggregate}Configuration.cs`: the `LocalizedTextConversion` converter and comparer, `jsonb` | `languages.md` | none | the configuration test |
| 4 | [languages.mode=data] the command takes the language from its context; the `Dto.Map` takes the caller's language | `languages.md` | AGENTS 20 | the handler test (`RenameProductCommandHandlerTests` is the shape) |
| 5 | [languages.mode=data] [database=postgres] a migration (the column becomes `jsonb`; existing text moves under the default language) | `infrastructure-postgres.md` "Migrations" | none | `languages.sh` |
| 6 | [languages.mode!=data] stop: translated data needs `languages.mode` = `data` (`turn-on-an-option.md`) | `languages.md` | R5 | none |
