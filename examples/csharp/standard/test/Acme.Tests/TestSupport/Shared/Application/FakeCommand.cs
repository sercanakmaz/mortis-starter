using System.Text.Json.Serialization;
using Acme.Shared.Application;
using FluentValidation;

namespace Acme.Tests.TestSupport.Shared.Application;

public record FakeCommand : ICommand<string>
{
    public string Name { get; set; } = "Default name";
}

public record FakeTransactionalCommand : ICommand<string>, ITransactionalCommand
{
    public string Name { get; set; } = "Default name";
}

public class FakeTransactionalCommandValidator : AbstractValidator<FakeTransactionalCommand>
{
    public FakeTransactionalCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty();
    }
}

public record FakeContextCommand : ICommand<string>, IContextCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public string Name { get; set; } = "Default name";
}
