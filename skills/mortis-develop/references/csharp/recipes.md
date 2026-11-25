# The recipes in C#

The commands and C# notes the recipes rely on. Paths in the recipes are already C#'s; this page adds what to run. `<skills>` is the folder holding the skills and `<project>` the project's: use absolute paths, since the work happens in the project.

## Commands

```bash
dotnet build                                   # 0 warnings is the bar
dotnet test                                    # the whole suite
dotnet test --filter "FullyQualifiedName~OrderTests" -p:CollectCoverage=false   # one class while working (the coverage gate counts the whole suite)
dotnet ef migrations add <Change> --project src/{Name}.Infrastructure --output-dir Persistence/Migrations
dotnet format
bash <skills>/mortis-scaffold/scripts/verify.sh <project>
bash <skills>/mortis-review/scripts/review.sh <project>
bash <skills>/mortis-develop/scripts/new_aggregate.sh <project> Invoice Invoices   # the files an aggregate needs in this project, by its MORTIS.md
```

A migration is named after the change in PascalCase (`AddInvoices`, `AddTenantToProducts`). Never edit a migration that was applied anywhere outside a developer's machine; add another.

## Notes per recipe

| Recipe | Note |
|---|---|
| `add-aggregate.md` | The repository is a sealed class deriving from the kernel's base repository; its constructor takes `AppDbContext`, `IUnitOfWork` and `EventContext` (`infrastructure-postgres.md`). The controller's route is `naming.routes` of `MORTIS.md` (`kebab-case` by default). |
| `add-child-entity.md` | EF Core maps the backing field with `builder.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field)`; `HasMany` with `OnDelete(DeleteBehavior.Cascade)`. |
| `add-value-object.md` | `ComplexProperty` for a single value object; `OwnsMany` for a list. |
| `add-command.md` | `[JsonIgnore] public CommandContext Context { get; set; }` is the one context line (AGENTS 20). The handler derives from `BaseCommandHandler<TCommand, TDto>` and receives `CommandPipeline<TCommand>`. |
| `add-sync-handler.md` | `ContextOf(@event)` gives the reaction's command the user, tenant, language and correlation of the event. |
| `add-async-handler.md` | `BaseEventConsumer<TEvent>` requires `TEvent : IQueueEvent`; the compiler refuses a consumer of an in-process event. |
| `change-a-key-type.md` | Route constraints change with the type: `{id:guid}`, `{id:int}`, `{id:long}`. |
| `turn-on-an-option.md` | `python3 -I ../mortis-scaffold/scripts/shape_for.py ../mortis-scaffold/references/csharp/kernel-shape.txt MORTIS.md` prints the kernel lines the project must have now. |
