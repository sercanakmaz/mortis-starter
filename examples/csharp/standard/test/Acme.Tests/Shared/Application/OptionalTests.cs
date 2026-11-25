using System.Text.Json;
using Acme.Shared.Application;
using Acme.Tests.TestSupport.Shared.Application;

namespace Acme.Tests.Shared.Application;

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
