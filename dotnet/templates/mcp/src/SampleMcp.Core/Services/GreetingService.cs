using SampleMcp.Core.Interfaces;

namespace SampleMcp.Core.Services;

public sealed class GreetingService : IGreetingService
{
    public string Greet(string name) => $"Hello, {name}!";
}
