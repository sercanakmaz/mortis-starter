using Boilerplate.Tests.TestSupport.Domain.Categories;
using Boilerplate.Domain.Aggregates.Categories.Events;

namespace Boilerplate.Tests.Domain.Categories;

public class CategoryTests
{
    [Fact]
    public void Constructor_Should_RaiseNothingBecauseTheIdIsNotKnownYet()
    {
        var fakeCategory = FakeCategory.Create(" Tools ");

        Assert.Equal("Tools", fakeCategory.Name);
        Assert.Equal(0, fakeCategory.Id);
        Assert.Empty(fakeCategory.DomainEvents ?? []);
    }

    [Fact]
    public void MarkCreated_WithTheDatabaseId_Should_RaiseCreatedCarryingIt()
    {
        var fakeCategory = FakeCategory.Create().WithId(42);

        fakeCategory.MarkCreated();

        Assert.Equal("42", Assert.IsType<CategoryCreated>(Assert.Single(fakeCategory.DomainEvents)).Id);
    }

    [Fact]
    public void MarkCreated_BeforeTheDatabaseAssignedAnId_Should_Throw()
    {
        var fakeCategory = FakeCategory.Create();

        Assert.Throws<InvalidOperationException>(() => fakeCategory.MarkCreated());
    }
}
