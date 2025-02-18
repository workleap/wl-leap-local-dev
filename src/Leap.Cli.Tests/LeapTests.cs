#pragma warning disable CA1849 // Call async methods when in an async method (CreateTextFile doesn't need to be async)
using Workleap.Leap.Testing;
using Meziantou.Extensions.Logging.Xunit;
using Meziantou.Framework;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Tests;
public sealed class LeapTests(ITestOutputHelper testOutputHelper)
{
    private LeapTestContext CreateContext(string? remoteEnvironmentName = null)
    {
        // Find local Leap executable
        var leapExecutablePath = FullPath.FromPath(typeof(LeapTests).Assembly.Location).Parent / "Workleap.Leap" + (OperatingSystem.IsWindows() ? ".exe" : "");
        if (!File.Exists(leapExecutablePath))
        {
            throw new InvalidOperationException($"Cannot find Leap executable at '{leapExecutablePath}'");
        }

        var factory = LoggerFactory.Create(builder => builder.AddProvider(new XUnitLoggerProvider(testOutputHelper)));
        return new LeapTestContext(factory, remoteEnvironmentName, CancellationToken.None)
        {
            KillExistingLeapInstancesOnStart = true,
            KillLeapInstanceOnStop = true,
            StartLeapTimeout = TimeSpan.FromMinutes(1),
            LeapExecutablePath = leapExecutablePath,
        };
    }

    [Fact]
    public async Task RunDockerService()
    {
        await using var context = this.CreateContext();

        await using var tempFolder = TemporaryDirectory.Create();
        context.AddConfigurationFiles(tempFolder.CreateTextFile("leap.yaml", """
            services:
              app:
                ingress:
                  host: app.workleap.localhost
                runners:
                - type: docker
                  image: mcr.microsoft.com/dotnet/samples:aspnetapp
                  containerPort: 8080
                  protocol: https
            """));

        context.AddHttpHealthCheckUrls(() => context.GetUrl("app"));

        await context.Start();
    }

    [Fact]
    public async Task RunDotnetService()
    {
        await using var context = this.CreateContext();

        await using var tempFolder = TemporaryDirectory.Create();
        await CliWrap.Cli.Wrap("dotnet")
            .WithArguments(["new", "webapi", "--name", "aspnetcorewebapi", "--output", tempFolder.FullPath])
            .ExecuteAsync();

        var csprojPath = tempFolder.FullPath / "aspnetcorewebapi.csproj";

        context.AddConfigurationFiles(tempFolder.CreateTextFile("leap.yaml", $$"""
            services:
              app:
                ingress:
                  host: app.workleap.localhost
                runners:
                - type: dotnet
                  project: {{csprojPath}}
            dependencies:
            - type: mongo
            """));

        context.AddHttpHealthCheckUrls(() => context.GetUrl("app", "weatherforecast"));
        await context.Start();

        Assert.Equal(new Uri("mongodb://127.0.0.1:27217/"), context.GetUrl("mongo"));
    }

    [Fact]
    public async Task RunRemoteService()
    {
        await using var context = this.CreateContext("dev");
        await using var tempFolder = TemporaryDirectory.Create();
        context.AddConfigurationFiles(tempFolder.CreateTextFile("leap.yaml", $$"""
            services:
              app:
                runners:
                - type: remote
                  environments:
                    dev: https://example.com/
                    stg: https://fhsdjfhaslkfhdsakjlfyhuweiohrfsdjklfhbasdl.com/
              app2:
                runners:
                - type: docker
                  image: whatever
                  containerPort: 8080
            dependencies:
            - type: mongo
            """));

        context.AddHttpHealthCheckUrls(() => context.GetUrl("app"));
        await context.Start();

        Assert.Equal(new Uri("https://example.com/"), context.GetUrl("app"));

        Assert.Throws<InvalidOperationException>(() => context.GetUrl("app2"));
        Assert.Throws<InvalidOperationException>(() => context.GetUrl("mongo"));
    }
}
