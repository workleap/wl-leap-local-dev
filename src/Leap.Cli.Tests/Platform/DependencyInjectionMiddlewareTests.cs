using System.CommandLine;
using System.CommandLine.Invocation;
using FakeItEasy;
using Leap.Cli.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Leap.Cli.Tests.Platform;

public class DependencyInjectionMiddlewareTests
{
    [Fact]
    public async Task Registered_Services_Are_Available_Through_BindingContext_Service_Provider()
    {
        var expectedDependency1 = new ExampleDependency();
        var expectedDependency2 = new ExampleDependency();

        void ConfigureServices(ServiceCollection services)
        {
            services.AddSingleton(expectedDependency1);
            services.AddSingleton(expectedDependency2);
        }

        var expectedConsole = A.Fake<IConsole>();
        var expectedInvocationContext = new InvocationContext(new RootCommand().Parse(), expectedConsole);
        var dependencyInjectionMiddleware = DependencyInjectionMiddleware.CreateDependencyInjectionMiddleware(ConfigureServices);

        Task AssertResolvedServices(InvocationContext context)
        {
            var actualConsole = context.BindingContext.GetRequiredService<IConsole>();
            Assert.Same(expectedConsole, actualConsole);

            var actualSingleDependency = context.BindingContext.GetRequiredService<ExampleDependency>();
            Assert.Same(expectedDependency2, actualSingleDependency);

            var actualEnumerableDependencies = context.BindingContext.GetServices<ExampleDependency>().ToArray();
            Assert.Equal(2, actualEnumerableDependencies.Length);
            Assert.Same(expectedDependency1, actualEnumerableDependencies[0]);
            Assert.Same(expectedDependency2, actualEnumerableDependencies[1]);

            return Task.CompletedTask;
        }

        await dependencyInjectionMiddleware(expectedInvocationContext, AssertResolvedServices);
    }

    private sealed class ExampleDependency
    {
    }
}