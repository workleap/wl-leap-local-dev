using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using Microsoft.Extensions.DependencyInjection;

namespace Leap.Cli.Platform;

internal static class DependencyInjectionMiddleware
{
    public static CommandLineBuilder UseDependencyInjection(this CommandLineBuilder builder, Action<ServiceCollection, InvocationContext> configureServices)
    {
        return builder.AddMiddleware(CreateDependencyInjectionMiddleware(configureServices), MiddlewareOrder.Configuration);
    }

    internal static InvocationMiddleware CreateDependencyInjectionMiddleware(Action<ServiceCollection, InvocationContext> configureServices)
    {
        async Task RegisterServiceProvider(InvocationContext context, Func<InvocationContext, Task> next)
        {
            // Register our services in the modern Microsoft dependency injection container
            var services = new ServiceCollection();
            configureServices(services, context);
            var uniqueServiceTypes = new HashSet<Type>(services.Select(x => x.ServiceType));

            await using var serviceProvider = services.BuildServiceProvider();

            // System.CommandLine's service provider is a "dumb" implementation that relies on a dictionary of factories,
            // but we can still make sure here that "true" dependency-injected services are available from "context.BindingContext".
            // https://github.com/dotnet/command-line-api/blob/2.0.0-beta4.22272.1/src/System.CommandLine/Invocation/ServiceProvider.cs
            context.BindingContext.AddService<IServiceProvider>(_ => serviceProvider);

            foreach (var serviceType in uniqueServiceTypes)
            {
                context.BindingContext.AddService(serviceType, _ => serviceProvider.GetRequiredService(serviceType));

                // Enable support for "context.BindingContext.GetServices<>()" as in the "true" Microsoft dependency injection
                var enumerableServiceType = typeof(IEnumerable<>).MakeGenericType(serviceType);
                context.BindingContext.AddService(enumerableServiceType, _ => serviceProvider.GetServices(serviceType));
            }

            await next(context);
        }

        return RegisterServiceProvider;
    }
}