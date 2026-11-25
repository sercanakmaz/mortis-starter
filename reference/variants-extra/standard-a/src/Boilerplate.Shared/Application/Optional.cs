using System.Text.Json.Serialization;

namespace Boilerplate.Shared.Application;

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
