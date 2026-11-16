namespace Boilerplate.Console.Scenarios;

public interface IScenario
{
    string Name { get; }
    Task RunAsync(string[] args);
}
