namespace Boilerplate.Shared.Domain;

public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTime? DeletedAt { get; }
    string DeletedUserId { get; }
}
