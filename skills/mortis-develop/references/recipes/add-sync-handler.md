# Add a sync event handler

A reaction that must hold together with the change: it runs inside the command's transaction, and a throw rolls the whole command back.

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | the reaction's command first, if it does not exist: `add-command.md` (this skill), named `{Verb}{Aggregate}When{Condition}Command` when it exists only for the reaction | `application.md` | AGENTS 9, 10 | its handler test |
| 2 | `test/.../Application/{Plural}/{Action}When{Aggregate}{Event}EventHandlerTests.cs`, in the reacting aggregate's folder | `events-inprocess.md` | AGENTS 14 | the handler sends exactly that command with `ContextOf(@event)` |
| 3 | `src/{Name}.Application/{Plural}/{Action}When{Aggregate}{Event}EventHandler.cs`: `BaseSyncEventHandler<TEvent>`, sends one command through `ICommandDispatcher` | `events-inprocess.md` | AGENTS 9, 10, 19 | step 2 green |
| 4 | `ApplicationModule.cs`: `AddScoped<BaseSyncEventHandler<TEvent>, THandler>()`; `Sequence` when order among handlers matters | `application.md` | AGENTS 13 | [testing=architecture] `RegistrationRulesTests` |
| 5 | [addons=loop-guard] nothing: the chain grows by itself; a handler that sends the command that raised its event is a loop | `loop-guard.md` | none | `LoopGuardTests` |

A handler never calls a repository or a domain service; it sends a command, so validation, logging and the transaction apply to the reaction too.
