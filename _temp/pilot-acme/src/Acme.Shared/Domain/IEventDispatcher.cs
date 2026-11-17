namespace Acme.Shared.Domain;

public interface IEventDispatcher
{
    Task Publish(EventBase @event);
}
