using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;
using System.Text.Json;

namespace Boilerplate.Tests.Shared.Application;

public class ContextCommandTests
{
    [Fact]
    public void Deserialize_WithAContextInTheJson_Should_IgnoreIt()
    {
        var fakeContextCommand = JsonSerializer.Deserialize<FakeContextCommand>("{\"name\":\"n\",\"context\":{\"user\":{\"id\":\"spoofed\"}},\"userId\":\"spoofed\"}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal("n", fakeContextCommand.Name);
        Assert.Null(fakeContextCommand.Context);
    }

    [Fact]
    public void Serialize_Should_NotWriteTheContext()
    {
        var json = JsonSerializer.Serialize(new FakeContextCommand { Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.DoesNotContain("user-1", json);
    }
}
