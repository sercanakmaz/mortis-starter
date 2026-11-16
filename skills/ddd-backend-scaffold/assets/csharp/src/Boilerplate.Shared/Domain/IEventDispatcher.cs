namespace Boilerplate.Shared.Domain;

public interface IEventDispatcher
{
    Task Publish(EventBase @event);
}
