using System.Text.Json;
using System.Text.Json.Serialization;

namespace Boilerplate.Shared.Application;

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
