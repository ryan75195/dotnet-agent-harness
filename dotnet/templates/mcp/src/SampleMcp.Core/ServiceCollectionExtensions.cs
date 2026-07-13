using SampleMcp.Core.Interfaces;
using SampleMcp.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace SampleMcp.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IGreetingService, GreetingService>();
        return services;
    }
}
