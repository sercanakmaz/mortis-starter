using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Domain.Aggregates.Categories;

namespace Boilerplate.Tests.TestSupport.Domain.Categories;

public class FakeCategory : Category
{
    private FakeCategory(string name) : base(name, FakeAuditUser.Create())
    {
    }

    public static FakeCategory Create(string name = "Default Category") => new(name);

    public FakeCategory WithId(int id)
    {
        Id = id;

        return this;
    }
}
