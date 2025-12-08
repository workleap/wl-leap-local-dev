#pragma warning disable CA1849 // Call async methods when in an async method (CreateTextFile doesn't need to be async)
using Meziantou.Extensions.Logging.Xunit;
using Meziantou.Framework;
using Microsoft.Extensions.Logging;
using Workleap.Leap.Testing;

namespace Leap.Cli.Tests;

public sealed class LeapTests(ITestOutputHelper testOutputHelper)
{
    private LeapTestContext CreateContext(string? remoteEnvironmentName = null, CancellationToken cancellationToken = default)
    {
        // Find local Leap executable
        var leapExecutablePath = FullPath.FromPath(typeof(LeapTests).Assembly.Location).Parent / "Workleap.Leap" + (OperatingSystem.IsWindows() ? ".exe" : "");
        if (!File.Exists(leapExecutablePath))
        {
            throw new InvalidOperationException($"Cannot find Leap executable at '{leapExecutablePath}'");
        }

        var factory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(new XUnitLoggerProvider(testOutputHelper));
            builder.SetMinimumLevel(LogLevel.Trace);
        });

        return new LeapTestContext(factory, remoteEnvironmentName, cancellationToken)
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
                healthcheck: /
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
                healthcheck: /weatherforecast
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

    [Fact]
    public async Task StartServicesExplicitly()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await using var context = this.CreateContext(cancellationToken: cts.Token);

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
                healthcheck: /weatherforecast
                runners:
                - type: dotnet
                  project: {{csprojPath}}
            dependencies:
            - type: mongo
            """));

        context.ExtraLeapRunArguments = ["--start-services-explicitly"];
        await context.Start();

        testOutputHelper.WriteLine("Checking services are not accessible");
        var url = context.GetUrl("app", "weatherforecast");
        Assert.False(await IsUrlAccessible(url, cts.Token));

        testOutputHelper.WriteLine("Starting services explicitly");
        await context.StartService("app");

        testOutputHelper.WriteLine("Stopping services explicitly");
        await context.StopService("app");

        testOutputHelper.WriteLine("Checking services are not accessible");
        Assert.False(await IsUrlAccessible(url, cts.Token));
    }

    private static readonly HttpClient HttpClient = new();
    private static async Task<bool> IsUrlAccessible(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await HttpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}