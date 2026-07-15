using Microsoft.Extensions.DependencyInjection;
using SampleDurable.Core.Interfaces;
using SampleDurable.Core.Services;

namespace SampleDurable.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton<IAgentDispatcher, StubAgentDispatcher>();
        services.AddSingleton<IResultPublisher, StubResultPublisher>();
        return services;
    }
}
