using Boilerplate.Contracts;
using Boilerplate.Application;
using Boilerplate.Domain;
using Boilerplate.Shared;

namespace Boilerplate.Tests.Architecture;

public class LayerRulesTests
{
    [Theory]
    [InlineData(typeof(SharedModule), new string[0])]
    [InlineData(typeof(ContractsMarker), new[] { nameof(Shared) })]
    [InlineData(typeof(DomainModule), new[] { nameof(Shared), nameof(Contracts) })]
    [InlineData(typeof(ApplicationModule), new[] { nameof(Shared), nameof(Contracts), nameof(Domain) })]
    public void Assembly_Should_ReferenceOnlyTheLayersBelowIt(Type moduleType, string[] allowedLayers)
    {
        var projectPrefix = typeof(SharedModule).Namespace!.Split('.')[0] + ".";

        var referencedLayers = moduleType.Assembly.GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name!)
            .Where(name => name.StartsWith(projectPrefix))
            .Select(name => name[projectPrefix.Length..]);

        Assert.Empty(referencedLayers.Except(allowedLayers));
    }
}
