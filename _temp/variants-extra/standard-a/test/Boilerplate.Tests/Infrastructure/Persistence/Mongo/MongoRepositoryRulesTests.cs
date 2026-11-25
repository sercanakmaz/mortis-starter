using Boilerplate.Tests.TestSupport;

namespace Boilerplate.Tests.Infrastructure.Persistence.Mongo;

public class MongoRepositoryRulesTests
{
    [Fact]
    public void Repositories_Should_QueryOnlyThroughTheBase()
    {
        var mongoFolder = Path.Combine(SolutionFolder.Find(), "src", "Boilerplate.Infrastructure", "Persistence", "Mongo");

        var offenders = Directory.GetFiles(mongoFolder, "*Repository.cs", SearchOption.AllDirectories)
            .Where(file => Path.GetFileName(file) != "MongoRepository.cs")
            .Where(file => File.ReadAllText(file).Contains("GetCollection<"));

        Assert.Empty(offenders);
    }
}
