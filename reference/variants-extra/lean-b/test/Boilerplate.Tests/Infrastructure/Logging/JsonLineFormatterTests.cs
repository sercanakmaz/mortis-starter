using Microsoft.Extensions.Logging.Abstractions;
using Boilerplate.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Boilerplate.Tests.Infrastructure.Logging;

public class JsonLineFormatterTests
{
    [Fact]
    public void Write_Should_WriteOneFlatJsonObjectWithTheCorrelationIdFirstAndTheScopeAndMessageFields()
    {
        var correlationId = Guid.CreateVersion7();
        var scopeProvider = new LoggerExternalScopeProvider();
        var writer = new StringWriter();
        var state = new Dictionary<string, object> { ["Command"] = "CreateProductCommand", ["{OriginalFormat}"] = "{Command} started" };

        using (scopeProvider.Push(new Dictionary<string, object> { [LoggingConstants.CorrelationId] = correlationId, ["UserId"] = "console" }))
        {
            new JsonLineFormatter().Write(new LogEntry<Dictionary<string, object>>(LogLevel.Information, "Category", default, state, null, (_, _) => "CreateProductCommand started"), scopeProvider, writer);
        }

        using var document = JsonDocument.Parse(writer.ToString());
        var properties = document.RootElement.EnumerateObject().ToList();
        Assert.Equal(LoggingConstants.CorrelationId, properties[0].Name);
        Assert.Equal(correlationId.ToString(), properties[0].Value.GetString());
        Assert.Equal("console", document.RootElement.GetProperty("UserId").GetString());
        Assert.Equal("CreateProductCommand", document.RootElement.GetProperty("Command").GetString());
        Assert.Equal("CreateProductCommand started", document.RootElement.GetProperty("Message").GetString());
        Assert.All(properties, property => Assert.NotEqual(JsonValueKind.Object, property.Value.ValueKind));
        Assert.DoesNotContain(properties, property => property.Name == "{OriginalFormat}");
    }

    [Fact]
    public void Write_WithAnException_Should_AddItAsOneTextField()
    {
        var writer = new StringWriter();

        new JsonLineFormatter().Write(new LogEntry<string>(LogLevel.Error, "Category", default, "state", new InvalidOperationException("boom"), (_, _) => "failed"), null, writer);

        using var document = JsonDocument.Parse(writer.ToString());
        Assert.Contains("boom", document.RootElement.GetProperty("Exception").GetString());
        Assert.Equal(LoggingConstants.CorrelationId, document.RootElement.EnumerateObject().First().Name);
    }
}
