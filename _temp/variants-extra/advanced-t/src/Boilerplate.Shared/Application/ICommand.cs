namespace Boilerplate.Shared.Application;

public interface ICommand;

public interface ICommand<TContent> : ICommand;

public interface ITransactionalCommand;
