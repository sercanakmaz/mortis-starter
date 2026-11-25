using Acme.Application;
using Acme.Shared.Application;
using Acme.Shared.Domain;
using Acme.Shared.Persistence;

namespace Acme.Tests.Application;

public class QueryRulesTests
{
    [Fact]
    public void QueryHandlers_Should_DependOnNoUnitOfWorkRepositoryOrDomainService()
    {
        var forbidden = new Func<Type, bool>(type =>
            type == typeof(IUnitOfWork)
            || type.GetInterfaces().Concat([type]).Any(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IRepository<,>))
            || type.Namespace?.StartsWith("Acme.Domain") == true && type.Name.EndsWith("Service"));

        var offenders = typeof(ApplicationModule).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && type.BaseType is { IsGenericType: true } baseType && baseType.GetGenericTypeDefinition() == typeof(BaseQueryHandler<,>))
            .SelectMany(type => type.GetConstructors().SelectMany(constructor => constructor.GetParameters()).Where(parameter => forbidden(parameter.ParameterType)).Select(parameter => $"{type.Name}({parameter.ParameterType.Name})"));

        Assert.Empty(offenders);
    }
}
