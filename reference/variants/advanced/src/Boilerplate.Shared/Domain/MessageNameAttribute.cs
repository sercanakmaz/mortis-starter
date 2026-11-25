namespace Boilerplate.Shared.Domain;

[AttributeUsage(AttributeTargets.Class)]
public sealed class MessageNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
