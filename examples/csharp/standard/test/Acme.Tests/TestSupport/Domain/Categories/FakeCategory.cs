using Acme.Domain.Aggregates.Categories;
using Acme.Tests.TestSupport.Shared.Domain;

namespace Acme.Tests.TestSupport.Domain.Categories;

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
