# Add a command

A command that writes (or, with `queries` = `commands`, a read). The handler is the use case: load, call, stamp the user, save, map.

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | `test/{Name}.Tests/Application/{Plural}/{Verb}{Aggregate}CommandHandlerTests.cs` | `worked-slice-tests.md` | AGENTS 14 | the aggregate's operation runs, the write is recorded in order inside the transaction, the user is stamped; an unknown id is `not_found` and writes nothing |
| 2 | `src/{Name}.Application/{Plural}/{Verb}{Aggregate}Command.cs`: the record (`ICommand<TDto>`, `IContextCommand`, `ITransactionalCommand` when it writes), its validator, its handler | `application.md` | AGENTS 5, 10, 11, 20 | step 1 green |
| 3 | `ApplicationModule.cs`: `AddCommandHandler<...>()` and the validator | `application.md` | AGENTS 13 | [testing=architecture] `RegistrationRulesTests` |
| 4 | the host: a controller action, or nothing (the Console runs commands by name) | `hosts.md` | AGENTS 10 | `verify.sh` |
| 5 | [testing=handlers\|integration\|architecture] `{Verb}{Aggregate}CommandValidatorTests.cs` | `testing-levels.md` | AGENTS 11 | invalid members fail on their name |
| 6 | [auth=jwt] the command's permission, once permissions are generated | `permissions.md` | none | `PermissionRulesTests` |
| 7 | [languages.mode!=none] a new error code in every `messages.{language}.json` | `languages.md` | R5 | the localizer tests |
| 8 | [exceptions=catalogued] a new error code in the catalogue | `failures.md` | none | `ErrorCatalogueTests` |
| 9 | [addons=api-conventions] a partial update is a `PATCH` with `Optional<T>` members | `api-conventions.md` | none | the handler tests with absent and null members |
| 10 | [addons=caching] a change to a cached answer raises an event with an invalidation handler | `caching.md` | none | the invalidation handler test |

A second aggregate that changes because of this command is an event handler (`add-sync-handler.md` or `add-async-handler.md`), never a second write here, unless the action is about both (decision 28).
