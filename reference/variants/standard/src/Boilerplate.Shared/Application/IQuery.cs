namespace Boilerplate.Shared.Application;

public interface IQuery
{
    CommandContext Context { get; set; }
}

public interface IQuery<TResult> : IQuery;
