using Boilerplate.Shared.Exceptions;
using Boilerplate.Console;

namespace Boilerplate.Tests.Console;

public class ConsoleArgumentsTests
{
    [Fact]
    public void Parse_Should_TakeTheContextFromTheOptionsAndThenTheCommandAndItsArgument()
    {
        var consoleArguments = ConsoleArguments.Parse(["--user", "user-1", "--email", "user-1@localhost", "--tenant", "tenant-1", "--language", "tr", "CreateProduct", "{}"]);

        Assert.Equal(["user-1", "user-1@localhost", "tenant-1", "tr", "CreateProduct", "{}"],
            [consoleArguments.CommandContext.User.Id, consoleArguments.CommandContext.User.Email, consoleArguments.CommandContext.TenantId, consoleArguments.CommandContext.Language, consoleArguments.Name, consoleArguments.Argument]);
    }

    [Fact]
    public void Parse_WithoutAUser_Should_ThrowInvalidParameter()
    {
        var exception = Assert.Throws<InvalidParameterException>(() => ConsoleArguments.Parse(["CreateProduct", "{}"]));

        Assert.StartsWith("--user is invalid", exception.Message);
    }

    [Fact]
    public void Parse_WithAnUnknownOptionOrNoCommand_Should_ThrowInvalidParameter()
    {
        Assert.Throws<InvalidParameterException>(() => ConsoleArguments.Parse(["--token", "x", "CreateProduct"]));
        Assert.Throws<InvalidParameterException>(() => ConsoleArguments.Parse(["--user", "user-1"]));
    }
}
