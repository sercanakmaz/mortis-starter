using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.TestSupport.Shared.Domain;

public static class FakeAuditUser
{
    public const string Id = "test-user-id";
    public const string Email = "test-user@localhost";

    public static AuditUser Create() => new(Id, Email);
    public static AuditUser CreateOther() => new("other-user-id", "other-user@localhost");
}
