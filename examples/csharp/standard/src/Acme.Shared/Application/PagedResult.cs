namespace Acme.Shared.Application;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int Size, int Total);
