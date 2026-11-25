using System.Reflection;
using System.Runtime.CompilerServices;
using Boilerplate.Application;
using Boilerplate.Domain;

namespace Boilerplate.Tests.Architecture;

public class ServiceRulesTests
{
    [Theory]
    [InlineData(typeof(DomainModule))]
    [InlineData(typeof(ApplicationModule))]
    public void Service_Should_DeclareNoPrivateMethod(Type moduleType)
    {
        var privateMethods = moduleType.Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Service") && type.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .SelectMany(type => type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.IsPrivate && !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}");

        Assert.Empty(privateMethods);
    }
}
