using Acme.Shared.Domain;

namespace Acme.Shared.Application;

public interface IUserCommand
{
    string UserId { get; set; }

    string UserEmail { get; set; }
}

public static class UserCommandExtensions
{
    public static AuditUser ToAuditUser(this IUserCommand userCommand) => new(userCommand.UserId, userCommand.UserEmail);
}
