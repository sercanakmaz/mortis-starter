namespace Acme.Shared.Application;

public interface IContextCommand
{
    CommandContext Context { get; set; }
}
