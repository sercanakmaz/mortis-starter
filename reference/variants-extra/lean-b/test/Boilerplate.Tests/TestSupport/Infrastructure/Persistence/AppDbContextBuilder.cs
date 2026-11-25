using Boilerplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Tests.TestSupport.Infrastructure.Persistence;

public class AppDbContextBuilder
{
    public static AppDbContextBuilder Create() => new();

    public AppDbContext Build()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=never-connected;Database=unit-tests")
            .Options;

        return new AppDbContext(options);
    }
}
