# API conventions, C#

Applies when `addons` contains `api-conventions`. Projects `{Name}.Shared` (folder `Application/`), `{Name}.Api` (folder `Conventions/`) and, with the example, the customer slice. Layer 1: `architecture/http-and-operations.md`, `options/addons.md`.

**Built** in the standard reference solution (decision 52): compiled, tested and verified there.

## How it fits together

```
request body       System.Text.Json with UnmappedMemberHandling.Disallow and enums as names
  does not parse     -> MVC model state -> InvalidModelStateResponseFactory -> 400 invalid_request { errors by JSON path }
  unknown member     -> the same 400, the member named in the message; a command opts out with [JsonUnmappedMemberHandling(Skip)]
command failure    ExceptionHandler, unchanged
no endpoint        UseStatusCodePages: 404 route_not_found, 405 method_not_allowed, in the error body
PATCH              members are Optional<T>: absent = None, null = Of(null), value = Of(value); the handler uses Or(current)
lists              PagedResult<T> (queries.md): { items, page, size, total }
OpenAPI            the one document AddOpenApi already maps (/openapi/v1.json); no versioning
```

- Every failure leaves in `ErrorResponseModel`; a client parses one shape.
- The opt-out is the framework's own attribute on the command type, so it is visible where the command is declared, and there is no global switch.
- Status code pages only touch an empty 404 or 405. A 401 or 403 written by authentication keeps its own handling (`authentication.md`), and a `NotFoundException` already has a body.
- How System.Text.Json treats a body member named like a `[JsonIgnore]` member (`context`) under `Disallow` is proved in phase 2; either way it never reaches the command.

## Files this add-on changes

| File | Change |
|---|---|
| `src/{Name}.Api/Program.cs` | `builder.Services.AddControllers(...).AddApiConventions();` and `app.UseApiConventions();` right after `app.UseExceptionHandler();` |
| `src/{Name}.Api/Middlewares/ApiConstants.cs` | the codes and messages below |
| `src/{Name}.Shared/Application/PageRequest.cs`, `PagedResult.cs` | added with `queries` = `commands` too (`queries.md`), for list commands |
| example: `Customer.cs`, `CustomerDto.cs`, `CustomerConfiguration.cs`, `CustomersController.cs`, `CreateCustomerCommand.cs` | the `PATCH` slice and the opt-out below; a migration adds `customers.note` |

`ApiConstants` gains:

```csharp
    public const string InvalidRequestCode = "invalid_request";
    public const string InvalidRequestMessage = "The request could not be read.";
    public const string RouteNotFoundCode = "route_not_found";
    public const string RouteNotFoundMessage = "No endpoint answers this address.";
    public const string MethodNotAllowedCode = "method_not_allowed";
    public const string MethodNotAllowedMessage = "This endpoint does not answer this method.";
```

## ApiConventionsModule

File `src/{Name}.Api/Conventions/ApiConventionsModule.cs`:

```csharp
public static class ApiConventionsModule
{
    public static IMvcBuilder AddApiConventions(this IMvcBuilder mvcBuilder)
    {
        mvcBuilder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        return mvcBuilder
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = InvalidRequest);
    }

    public static WebApplication UseApiConventions(this WebApplication app)
    {
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var response = statusCodeContext.HttpContext.Response;

            if (response.StatusCode is not (StatusCodes.Status404NotFound or StatusCodes.Status405MethodNotAllowed))
            {
                return;
            }

            await response.WriteAsJsonAsync(response.StatusCode == StatusCodes.Status404NotFound
                ? new ErrorResponseModel(ApiConstants.RouteNotFoundCode, ApiConstants.RouteNotFoundMessage)
                : new ErrorResponseModel(ApiConstants.MethodNotAllowedCode, ApiConstants.MethodNotAllowedMessage));
        });

        return app;
    }

    private static IActionResult InvalidRequest(ActionContext actionContext)
    {
        var errors = actionContext.ModelState
            .Where(entry => entry.Value.Errors.Count > 0)
            .ToDictionary(entry => entry.Key.TrimStart('$', '.'), entry => entry.Value.Errors.Select(error => error.ErrorMessage).ToArray());

        return new BadRequestObjectResult(new ErrorResponseModel(ApiConstants.InvalidRequestCode, ApiConstants.InvalidRequestMessage, errors));
    }
}
```

- `ConfigureHttpJsonOptions` is the options object `WriteAsJsonAsync` and the OpenAPI document read; `AddJsonOptions` is the one MVC binds bodies with. Both get the enum converter so the document and the wire agree.
- The unknown-member message comes from System.Text.Json ("The JSON property 'nmae' could not be mapped to any .NET member contained in type ..."): MVC keeps formatter messages by default (`AllowInputFormatterExceptionMessages`).

## Optional

File `src/{Name}.Shared/Application/Optional.cs`:

```csharp
[JsonConverter(typeof(OptionalJsonConverterFactory))]
public readonly record struct Optional<T>
{
    public bool HasValue { get; }
    public T Value { get; }

    private Optional(T value)
    {
        HasValue = true;
        Value = value;
    }

    public static Optional<T> None => default;

    public static Optional<T> Of(T value) => new(value);

    public T Or(T current) => HasValue ? Value : current;
}
```

File `src/{Name}.Shared/Application/OptionalJsonConverterFactory.cs`:

```csharp
public sealed class OptionalJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return (JsonConverter)Activator.CreateInstance(typeof(OptionalJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]));
    }
}
```

File `src/{Name}.Shared/Application/OptionalJsonConverter.cs`:

```csharp
public sealed class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
{
    public override bool HandleNull => true;

    public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Optional<T>.Of(reader.TokenType == JsonTokenType.Null ? default : JsonSerializer.Deserialize<T>(ref reader, options));
    }

    public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            JsonSerializer.Serialize(writer, value.Value, options);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
```

- An absent member never reaches the converter, so it keeps `default`: `None`. `HandleNull` makes an explicit `null` reach `Read`, which returns `Of(null)`.
- The attribute on the type makes every serializer honour it (MVC, the Console's arguments, the outbox), with no registration.
- A validator checks the value only when present: `RuleFor(command => command.Name.Value).NotEmpty().MaximumLength(CustomerConstants.NameMaxLength).When(command => command.Name.HasValue).OverridePropertyName(nameof(ChangeCustomerCommand.Name));`.

## The example: PATCH a customer

The customer gains a nullable `Note` so `PATCH` has a member to clear. `Customer.cs`:

```csharp
    public string Note { get; protected set; }

    public void Change(string name, string note)
    {
        Name = name;
        Note = note;

        RaiseEvent(new Changed(Id.ToString()));
    }
```

`Events/Changed.cs` is `public record Changed(string Id) : EventBase;`, like `Blocked`. `CustomerConstants` gains `NoteMaxLength = 500`, `CustomerConfiguration` maps `Note` with that length, `CustomerDto` gains `Note`.

File `src/{Name}.Application/Customers/ChangeCustomerCommand.cs`:

```csharp
public record ChangeCustomerCommand : ICommand<CustomerDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
    [JsonIgnore] public Guid Id { get; set; }

    public Optional<string> Name { get; set; }
    public Optional<string> Note { get; set; }
}

public class ChangeCustomerCommandValidator : AbstractValidator<ChangeCustomerCommand>
{
    public ChangeCustomerCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name.Value).NotEmpty().MaximumLength(CustomerConstants.NameMaxLength).When(command => command.Name.HasValue).OverridePropertyName(nameof(ChangeCustomerCommand.Name));
        RuleFor(command => command.Note.Value).MaximumLength(CustomerConstants.NoteMaxLength).When(command => command.Note.HasValue).OverridePropertyName(nameof(ChangeCustomerCommand.Note));
    }
}

public class ChangeCustomerCommandHandler(
    ICustomerRepository customerRepository,
    CommandPipeline<ChangeCustomerCommand> commandPipeline) : BaseCommandHandler<ChangeCustomerCommand, CustomerDto>(commandPipeline)
{
    protected override async Task<CommandResult<CustomerDto>> OnHandle(ChangeCustomerCommand command)
    {
        var customer = await customerRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Customer));

        customer.Change(command.Name.Or(customer.Name), command.Note.Or(customer.Note));
        customer.SetUpdatedAudit(command.Context.User);

        await customerRepository.UpdateAsync(customer);

        return CommandResult.FromContent(CustomerDto.Map(customer));
    }
}
```

Its `Id` is `[JsonIgnore]`, unlike a command the Console runs (`worked-slice.md`): the route gives it, and a body member must never change which customer is changed. So the Console cannot run `ChangeCustomer`; a partial change is an HTTP operation.

Registered next to the other customer handlers in `ApplicationModule`. `CustomersController`:

```csharp
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Change([FromRoute] Guid id, [FromBody] ChangeCustomerCommand command)
    {
        var commandResult = await commandDispatcher.Send(command with { Id = id });

        return Ok(commandResult.Content);
    }
```

The opt-out, on `CreateCustomerCommand` (a sign-up form that posts extra fields):

```csharp
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public record CreateCustomerCommand : ICommand<CustomerDto>, IContextCommand, ITransactionalCommand
```

## Tests

This option adds four test files: `OptionalProbe.cs`, `OptionalTests.cs`, `ApiConventionsModuleTests.cs`, `ChangeCustomerCommandHandlerTests.cs` (the last with the example). The manifest lists each; leaving one out fails the structure check.

`test/{Name}.Tests/TestSupport/Shared/Application/OptionalProbe.cs`, the record the tests deserialize (a test class holds only tests):

```csharp
public record OptionalProbe(Optional<string> Note);
```

`test/{Name}.Tests/Shared/Application/OptionalTests.cs`:

```csharp
public class OptionalTests
{
    [Fact]
    public void Deserialize_WithTheMemberAbsent_Should_LeaveItUnset()
    {
        var change = JsonSerializer.Deserialize<OptionalProbe>("{}");

        Assert.False(change.Note.HasValue);
        Assert.Equal("kept", change.Note.Or("kept"));
    }

    [Fact]
    public void Deserialize_WithNull_Should_SetItToNull()
    {
        var change = JsonSerializer.Deserialize<OptionalProbe>("""{"Note":null}""");

        Assert.True(change.Note.HasValue);
        Assert.Null(change.Note.Or("kept"));
    }

    [Fact]
    public void Deserialize_WithAValue_Should_SetIt()
    {
        var change = JsonSerializer.Deserialize<OptionalProbe>("""{"Note":"new"}""");

        Assert.Equal("new", change.Note.Or("kept"));
    }
}
```

`test/{Name}.Tests/Api/Conventions/ApiConventionsModuleTests.cs`:

```csharp
public class ApiConventionsModuleTests
{
    [Fact]
    public void InvalidModelStateResponseFactory_Should_AnswerTheErrorBodyKeyedByMember()
    {
        var services = new ServiceCollection();
        services.AddControllers().AddApiConventions();
        var apiBehaviorOptions = services.BuildServiceProvider().GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        actionContext.ModelState.AddModelError("$.nmae", "The JSON property 'nmae' could not be mapped.");

        var result = Assert.IsType<BadRequestObjectResult>(apiBehaviorOptions.InvalidModelStateResponseFactory(actionContext));

        var errorResponseModel = Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal(ApiConstants.InvalidRequestCode, errorResponseModel.Code);
        Assert.Contains("nmae", errorResponseModel.Errors.Keys);
    }

    [Fact]
    public void AddApiConventions_Should_RefuseUnknownMembersAndWriteEnumsAsNames()
    {
        var services = new ServiceCollection();
        services.AddControllers().AddApiConventions();
        var jsonSerializerOptions = services.BuildServiceProvider().GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

        Assert.Equal(JsonUnmappedMemberHandling.Disallow, jsonSerializerOptions.UnmappedMemberHandling);
        Assert.Equal("\"Friday\"", JsonSerializer.Serialize(DayOfWeek.Friday, jsonSerializerOptions));
    }
}
```

`test/{Name}.Tests/Application/Customers/ChangeCustomerCommandHandlerTests.cs`, the whole class (the validation test sends through a pipeline built with the real validator):

```csharp
public class ChangeCustomerCommandHandlerTests
```

```csharp
[Fact]
public async Task Handle_WithOnlyANullNote_Should_ClearTheNoteAndKeepTheName()
{
    Customer writtenCustomer = null;
    var customer = FakeCustomer.Create().WithoutEvents();
    customer.Change("Ada", "call before noon");
    var customerRepository = CustomerRepositoryBuilder.Create()
        .WithFindByIdTrackingAsync(customer)
        .WithUpdateAsync(updatedCustomer => writtenCustomer = updatedCustomer)
        .Build();
    var commandHandler = new ChangeCustomerCommandHandler(customerRepository, CommandPipelineBuilder<ChangeCustomerCommand>.Create().WithValidator(new ChangeCustomerCommandValidator()).Build());

    await commandHandler.Handle(new ChangeCustomerCommand { Id = customer.Id, Note = Optional<string>.Of(null), Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

    Assert.Equal("Ada", writtenCustomer.Name);
    Assert.Null(writtenCustomer.Note);
}
```

```csharp
[Fact]
public async Task Handle_WithNoMembers_Should_KeepEverything()
{
    Customer writtenCustomer = null;
    var customer = FakeCustomer.Create().WithoutEvents();
    customer.Change("Ada", "call before noon");
    var customerRepository = CustomerRepositoryBuilder.Create()
        .WithFindByIdTrackingAsync(customer)
        .WithUpdateAsync(updatedCustomer => writtenCustomer = updatedCustomer)
        .Build();
    var commandHandler = new ChangeCustomerCommandHandler(customerRepository, CommandPipelineBuilder<ChangeCustomerCommand>.Create().WithValidator(new ChangeCustomerCommandValidator()).Build());

    await commandHandler.Handle(new ChangeCustomerCommand { Id = customer.Id, Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

    Assert.Equal(("Ada", "call before noon"), (writtenCustomer.Name, writtenCustomer.Note));
}
```

```csharp
[Fact]
public async Task Handle_WithANullName_Should_FailValidationOnName()
{
    var customerRepository = CustomerRepositoryBuilder.Create().WithFindByIdTrackingAsync(FakeCustomer.Create().WithoutEvents()).Build();
    var commandHandler = new ChangeCustomerCommandHandler(customerRepository, CommandPipelineBuilder<ChangeCustomerCommand>.Create().WithValidator(new ChangeCustomerCommandValidator()).Build());

    var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => commandHandler.Handle(new ChangeCustomerCommand { Id = Guid.CreateVersion7(), Name = Optional<string>.Of(null) }));

    Assert.Contains(nameof(ChangeCustomerCommand.Name), exception.Errors.Keys);
}
```

```csharp
[Fact]
public async Task Handle_WithAnUnknownCustomer_Should_ThrowNotFound()
{
    var customerRepository = CustomerRepositoryBuilder.Create().WithFindByIdTrackingAsync(null).Build();
    var commandHandler = new ChangeCustomerCommandHandler(customerRepository, CommandPipelineBuilder<ChangeCustomerCommand>.Create().Build());

    await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new ChangeCustomerCommand { Id = Guid.CreateVersion7(), Context = new CommandContext() }));
}
```

`scripts/conformance/api-conventions.sh` proves the rest over HTTP.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `Optional.cs`, `OptionalJsonConverterFactory.cs`, `OptionalJsonConverter.cs` | `{Name}.Shared.Application` | `System.Text.Json`, `System.Text.Json.Serialization` |
| `ApiConventionsModule.cs` | `{Name}.Api.Conventions` | `{Name}.Api.Middlewares`, `Microsoft.AspNetCore.Mvc`, `System.Text.Json.Serialization` |
| `ChangeCustomerCommand.cs` | `{Name}.Application.Customers` | `System.Text.Json.Serialization`, `{Name}.Domain.Aggregates.Customers`, `{Name}.Shared.Application`, `{Name}.Shared.Exceptions`, `FluentValidation` |
| `OptionalTests.cs` | `{Name}.Tests.Shared.Application` | `{Name}.Shared.Application`, `System.Text.Json` |
| `ApiConventionsModuleTests.cs` | `{Name}.Tests.Api.Conventions` | `{Name}.Api.Conventions`, `{Name}.Api.Middlewares`, `Microsoft.AspNetCore.Http`, `Microsoft.AspNetCore.Mvc`, `Microsoft.AspNetCore.Mvc.Abstractions`, `Microsoft.AspNetCore.Routing`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options`, `System.Text.Json`, `System.Text.Json.Serialization` |

## Decision record

`docs/adr/0020-one-shape-for-requests-and-failures.md`, in `project-docs-adr.md`.
