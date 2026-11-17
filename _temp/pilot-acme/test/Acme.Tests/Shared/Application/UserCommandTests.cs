using System.Text.Json;
using Acme.Shared.Application;
using Acme.Tests.TestSupport.Shared.Application;

namespace Acme.Tests.Shared.Application;

public class UserCommandTests
{
    [Fact]
    public void ToAuditUser_Should_CarryTheUserIdAndEmail()
    {
        var fakeUserCommand = new FakeUserCommand { UserId = "user-1", UserEmail = "user-1@localhost" };

        var auditUser = fakeUserCommand.ToAuditUser();

        Assert.Equal(["user-1", "user-1@localhost"], [auditUser.Id, auditUser.Email]);
    }

    [Fact]
    public void Deserialize_WithUserFieldsInTheJson_Should_IgnoreThem()
    {
        var fakeUserCommand = JsonSerializer.Deserialize<FakeUserCommand>("{\"name\":\"n\",\"userId\":\"spoofed\",\"userEmail\":\"spoofed@localhost\"}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.Equal(["n", null, null], [fakeUserCommand.Name, fakeUserCommand.UserId, fakeUserCommand.UserEmail]);
    }

    [Fact]
    public void Serialize_Should_NotWriteTheUserFields()
    {
        var json = JsonSerializer.Serialize(new FakeUserCommand { UserId = "user-1", UserEmail = "user-1@localhost" });

        Assert.DoesNotContain("user-1", json);
    }
}
