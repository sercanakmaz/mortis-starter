using System.Text.Json.Serialization;
using Boilerplate.Shared.Application;
using FluentValidation;

namespace Boilerplate.Tests.TestSupport.Shared.Application;

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

public record FakeUserCommand : ICommand<string>, IUserCommand
{
    [JsonIgnore] public string UserId { get; set; }
    [JsonIgnore] public string UserEmail { get; set; }

    public string Name { get; set; } = "Default name";
}
