namespace Acme.Shared.Application;

public interface IQueryDispatcher
{
    Task<TResult> Ask<TResult>(IQuery<TResult> query);
}
