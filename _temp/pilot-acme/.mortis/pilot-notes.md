# Pilot notes (Acme, lean, csharp, postgres)

## SKILL.md (mortis-scaffold)
- Step table / step 1: root files .editorconfig etc. are listed but the table says build is the "done" test; at step 1 build passes with leftover template Program.cs/appsettings.json in Api and Console (overwritten at step 8). Not stated; fine.
- Step 1: "docs/adr/" and ".mortis/" - manifest always_allowed covers .mortis/**; SKILL.md does not say progress.md is exempt from extra-file check (manifest does). OK.
- Step 1 build-fix cycles so far: 0.

## solution.md
- Project files: the nine fenced csproj blocks are in table order but not labelled with which project each belongs to (7 blocks, order inferred from table: Shared, Domain, Application, Infrastructure, Api, Console, Tests). Inferred from content/ordering.
- docker-compose / Dockerfile: {database} for Acme = acme; fine.

## shared-domain.md
- Entity/AggregateRoot etc: usings are not given per file except where "Needs" says; I added Microsoft.Extensions.DependencyInjection to EventHandlerExecutor (GetServices) - not mentioned. Minor.
- BaseAsyncEventHandler: "same with IAsyncEventHandler" - the interface members are implied identical to ISyncEventHandler; inferred.
- AuditUser/SystemUser: contents clear. Constants and signatures all given; no invention.

## SKILL.md vs shared-application.md (ordering contradiction)
- Step 3 says "Done when build" but CommandPipeline needs ValidationFailedException and IUnitOfWork, which are step 4 (shared-other.md). Step 3 cannot build alone; I wrote steps 3 and 4 together.

## shared-application.md
- IUserCommand.cs: needs `using {Name}.Shared.Domain;` for AuditUser; not stated (only CommandPipeline's usings are listed). Added.
- CommandResult.cs: "two classes" snippets given; FromContent is placed under CommandResult - fine.
- ICommand.cs: "and `public interface ICommand<TContent> : ICommand;`" given inline, ok.
- Steps 3 build in isolation impossible (see SKILL.md note). Steps 2-4 built green together: 0 cycles.

## shared-other.md
- ExceptionBase snippets: ctor primary-param only; subclass snippets are complete. No gaps. Snippet blocks per type have no `namespace` line; assumed file-scoped from header.
- SharedModule.cs: usings not listed (Acme.Shared.Application, .Domain, DI); added.

## domain.md / application.md (step 5)
- Step 5 "empty bodies": domain.md's snippet for DomainModule registers an ICreditPolicy illustration (a type that does not exist); text says "with none, only `return services;`". Wrote `return services;`. Ambiguous snippet (shows illustration, not the empty body); ApplicationModule has no full snippet at all (only 4 loose registration lines and the "only return services" sentence); I had to invent the signature `AddApplicationModule(this IServiceCollection services)` by analogy with AddSharedModule/AddDomainModule.
- DomainModule/ApplicationModule: `using Microsoft.Extensions.DependencyInjection;` not stated.

## infrastructure-postgres.md (steps 6-7)
- SKILL.md step 6 "done when build" but the InfrastructureModule snippet registers LoggingEventDispatcher and AsyncEventHandlerDispatcher (step 7, events-inprocess.md). Step 6 cannot build alone, same as 3/4. I wrote 6 and 7 together.
- No page lists usings for infrastructure files at all (only "Needs using Microsoft.EntityFrameworkCore.Storage" and "using Npgsql"). I added Microsoft.EntityFrameworkCore, Microsoft.Extensions.Logging, .Configuration, .DependencyInjection, Acme.Shared.* by inference. Built first try.
- PersistenceModule: the snippet shows `services.AddScoped<I{Aggregate}Repository, {Aggregate}Repository>();` while "with no aggregate only return services". When example=true, step 11 adds the 3 repo lines; fine, but the page says the module has "one line per repository" - I wrote the empty form first.
- ConfigurationExtensions: explicit generic args are needed at call sites (`AggregateConfiguration<Order, Guid>()`) - given by the snippet. OK.
- Migration section says dotnet-ef install --global; already installed in this env.
- events-inprocess.md: no gaps; DbUpdateConcurrencyException needs Microsoft.EntityFrameworkCore using (not mentioned).

## hosts.md (step 8)
- ScenarioModule snippet and the launch.json example configs reference OrderFlowScenario/CreateOrderCommand/stock-flow (example slice, step 11). At step 8 they do not exist. I wrote ScenarioModule as only `return services;` (page says so for "no aggregate") and wrote launch.json with all four configs (example=true) since it is data. Page does not say to build the module in two stages; I inferred.
- ScenarioConstants snippets for OutputOptions/InputOptions have no trailing `;` (`... new() { WriteIndented = true }`). Added `;`. Also no `using System.Text.Json;` stated.
- Api/Console snippets: usings not listed for UserCommandFilter (ClaimTypes, Mvc.Filters, Shared.Application), ExceptionHandler (System.Net, Diagnostics, Shared.Exceptions), CorrelationIdMiddleware (System.Diagnostics), CommandScenario*, ConsoleUserExtensions. All inferred; every one needed.
- Program.cs for Api/Console: whole file given (good). dotnet new's Api appsettings.json (AllowedHosts) is overwritten by the page's content - not said explicitly that template appsettings.json must be replaced; the manifest lists it as data.
- Console project has namespace Acme.Console which shadows System.Console; page handles it by writing System.Console. Good. Scenario folder files: CommandScenario.cs "the scenario that runs one command" - fine.
- launch.json: I generated from the page's content with python (formatting identical in structure).

## testing.md / testing-kernel.md (step 9)
- Namespaces of test files are never stated anywhere in these pages. I inferred `Acme.Tests.TestSupport.<mirrored path>` and `Acme.Tests.<mirrored path>` from "mirrors src". Worked first try, but a page statement is needed (also the test project's `using Xunit` is global via csproj - that is stated only in solution.md).
- Usings for all test files are not listed (Moq, FluentValidation, Microsoft.AspNetCore.Mvc.Abstractions/Filters/Routing for ActionExecutingContextBuilder, Microsoft.EntityFrameworkCore for DbUpdateConcurrencyException in tests, Microsoft.Extensions.Logging for LogLevel). Inferred all; the ActionDescriptor namespace (Microsoft.AspNetCore.Mvc.Abstractions) is the hardest to guess.
- FakeCommand.cs "holds four types": FakeCommand, FakeTransactionalCommand, FakeTransactionalCommandValidator, FakeUserCommand. Snippets for FakeTransactionalCommand are prose only ("same Name property"); order of the four types is inferred from the snippet order (FakeCommand, FakeTransactionalCommand, validator, FakeUserCommand).
- Handlers: "FakeTransactionalCommandHandler.cs and FakeUserCommandHandler.cs are the same" - fine. Which file holds ThrowingEventHandler: "RecordingEventHandler.cs holds two handlers" - ok; RecordingAsyncEventHandler is a separate file per manifest, page lists it under the same heading without saying the file name explicitly (inferred from manifest).
- File names for test-support Shared/Domain: "FakeAuditUser" has no "File ..." line; inferred FakeAuditUser.cs. Same for FakeAggregate.cs, EventHandlerExecutorBuilder.cs, UnitOfWorkBuilder.cs, AppDbContextBuilder.cs, RecordingLogger.cs, ActionExecutingContextBuilder.cs, the pipeline/dispatcher/middleware builders - only manifest gives the names.
- testing-kernel.md: test classes' folder is given by heading (Shared/Domain etc.) - fine. Contradiction candidates: none; tests ran green 44/44 on first run.
- testing.md says "No test talks to a database" but AppDbContextBuilder builds a real Npgsql context - consistent (never opened).

## project-docs*.md (step 10)
- Pages are excellent for mechanical copy (~~~~markdown fences). The "{Name}" placeholder in adr 0005 line 196 is literal text in the page (`src/{Name}.Console` inside a "{Name}" substitution zone) - see check below.
- project-docs-adr.md: 0006 Date is the placeholder "<the date the project was generated>" - the page intro says it is the generation day, but the 0001-0005 dates are hard-coded 2026-11-16 (a stale date, differs from the generation day). Ambiguity: replaced only 0006 with today's date.
- SKILL.md step 10 says "copy MORTIS.md in" - but MORTIS.md was already written in the project root by the architect (the pilot wrote it directly into /tmp/pilot/Acme); no copy needed. Also project-docs.md says MORTIS.md "is copied into the project root unchanged" - consistent.
- AGENTS.md §5 mentions `CreateProduct` and README too; kept since example=true.
- Docs refer to skills `mortis-develop` / `mortis-review` that are not installed here (fine).

## worked-slice.md (read before step 11)
- "Order of work: Product, Customer, Order ... building after each" is impossible as stated: Product has DecreaseStockWhenOrderLineAddedEventHandler/IncreaseStockWhenOrderLineCancelledEventHandler (need Order's LineAdded/LineCancelled), Customer has RegisterCompletedOrderWhenOrderCompletedEventHandler (needs Order's Completed). The three event handlers (and their tests, and their registration lines) must be written after Order's domain events exist. Page should say "write event handlers after Order's events".
- cwd slip: a failed cd wrote Domain files into the skills repo root; moved into the project immediately (my mistake, not a skill gap)

## worked-slice.md / worked-slice-tests.md (step 11) - members I had to invent
- No snippet exists for: CreateProduct/CreateCustomer/BlockCustomer/RegisterCompletedOrder/IncreaseProductStock/AddOrderLine/CancelOrderLine handlers (only table rows). Wrote from "the shapes in application.md" - OK because tables list loads/calls/saves. Property lists of CreateProductCommand etc. come from table; validators for Create/Get/Block/Register/Increase: table describes the rules in prose only ("sku required within length; quantity > 0"), `DecreaseProductStockCommandValidator` snippet given, Increase said "as above". Exact rule chain (NotEmpty().MaximumLength(...), GreaterThanOrEqualTo(0)) invented by analogy.
- Commands: positional vs property form is stated for CompleteOrderCommand(Guid Id) with `[JsonIgnore]` body properties - there is no snippet showing a positional record WITH a body (BlockCustomerCommand/RegisterCompletedOrderCommand/CancelOrderWhenAllLinesCancelledCommand all need it). I inferred `public record X(Guid Id) : ..., IUserCommand, ITransactionalCommand { [JsonIgnore] props }`.
- Which commands implement IUserCommand/ITransactionalCommand: Get* no; others yes - derived from rule text; CancelOrderWhenAllLinesCancelledCommand is a "reaction" - table says UpdateAsync only when true, so transactional - inferred.
- Application files' usings never listed; namespace `{Name}.Application.{Plural}` inferred from naming.md "namespaces follow the folder path".
- ProductsController / CustomersController: only class lines `public class ProductsController(...)`, [ApiController]/[Route("products")] shown only for Orders. Route for Customers/Products inferred ("products","customers"; naming.routes=kebab-case single word).
- OrderLineModel in Domain but used by Application commands: ok.
- Events in 3 aggregates are all named `Created` (and Products/Customers/Orders namespaces `...Events`). Handlers import only the Orders events namespace so no clash; the page says "alias when another aggregate has an event of the same name" but never required it here. OK.
- Handler tests: only CreateOrder and CompleteOrder are fully shown, one DecreaseStock event-handler test shown, 12 other handler test classes + 3 event-handler classes + second tests are given as test NAMES only. I wrote all bodies: AddOrderLine, CancelOrderLine, CancelOrderWhenAllLinesCancelled, Create/Block/RegisterCompletedOrder, Create/Increase/Decrease product, 4 event-handler classes. Assertions invented (values: totals 18m, stock 13/7, etc.). Biggest invention area.
- worked-slice-tests.md "Handle_Should_RunTheCommandAsTheSystemUser..." - what to assert (UpdatedUserId == SystemUser.Id) not stated; invented.
- Tests page: `GetService<IDesignTimeModel>()` needs `Microsoft.EntityFrameworkCore.Infrastructure` + `Microsoft.EntityFrameworkCore.Metadata`; unlisted. (worked out in first compile)
- AppDbContextBuilder / ConfigurationTests: all compiled first try.
- Repository builder for Customer/Product described as "same members for Customer" - generated by template, fine.
- Console scenarios: StockFlowScenario RunAsync snippet is split in two code fences ("The member continues:") - merge needed; inferred. Scenario usings (System.Text.Json, Application.*, Domain.Aggregates.Orders for OrderLineModel) not listed.
- Scenario names in launch.json (stock-flow/order-flow) vs blocked-customer scenario: launch.json lacks blocked-customer - consistent with page.
- Step 11 ordering ("tests first, build after each step") was not followed to the letter: Product/Customer cannot build without Order events (see above), so I wrote all domain/app/infra, then tests, then built.

## verify.sh results
- FAIL kernel shape #1 (project fix applied): shared-application.md "Command contracts" says "three marker interfaces: `ICommand;`, `ITransactionalCommand;` and ... `ICommand<TContent> : ICommand;`" - the page order is ICommand, ITransactionalCommand, ICommand<TContent>, but kernel-shape.txt (and verify) require ICommand, ICommand<TContent>, ITransactionalCommand (declaration order is compared). Reordered the file. Contradiction between page prose order and kernel-shape.txt. Pages should state the file order of multi-type files (ICommand.cs, FakeCommand.cs, EventBase.cs...).
- FAIL kernel shape #2 (NOT fixed, skill defect): kernel-shape.txt line 72 hardcodes `Database=boilerplate` in PersistenceConstants.DesignTimeConnectionString, while solution.md/infrastructure-postgres.md say `{database}` = name lower-cased (acme) and must match appsettings/compose. verify.sh substitutes only `{Name}`, so the check can only pass for a project literally named Boilerplate. Proved: with the constant temporarily set to `boilerplate` verify prints "12 passed, 0 failed, 0 skipped"; I reverted to `acme` as the pages require. Fix belongs in kernel-shape.txt/verify.sh (use a `{database}` token).
- No build failure occurred in steps 1-13 (every build green first time). Test failures: none. Conformance (database/console/http) all pass against real PostgreSQL.
- Step 13 `dotnet format` made no changes; no instructions on whether to run `dotnet format --verify-no-changes` vs plain; plain run produced nothing.
- SKILL.md rule "Write exactly the files manifest.json lists": manifest covers .mortis/** via always_allowed only (not stated in SKILL.md).
- SKILL.md step 8 "Hosts: Api and Console, VS Code files" - done-when "build" cannot see launch.json correctness; verify has "editor files" check that is not described in the SKILL.md check list order (it lists tests/formatter but "editor files" appears in output).
