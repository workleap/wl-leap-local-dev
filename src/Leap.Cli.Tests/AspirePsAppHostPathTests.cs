#pragma warning disable CA1849 // Call async methods when in an async method
using System.Text;
using System.Text.Json;
using CliWrap;
using Meziantou.Extensions.Logging.Xunit;
using Meziantou.Framework;
using Microsoft.Extensions.Logging;
using Workleap.Leap.Testing;

namespace Leap.Cli.Tests;

public sealed class AspirePsAppHostPathTests(ITestOutputHelper testOutputHelper)
{
    private LeapTestContext CreateContext(string? remoteEnvironmentName = null, CancellationToken cancellationToken = default)
    {
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
            StartLeapTimeout = TimeSpan.FromMinutes(2),
            LeapExecutablePath = leapExecutablePath,
        };
    }

    [Fact]
    public async Task AspirePs_AppHostPath_ShouldBeFirstLeapYamlFilePath()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await using var context = this.CreateContext(remoteEnvironmentName: "dev", cancellationToken: cts.Token);

        await using var tempFolder = TemporaryDirectory.Create();
        var leapYamlPath = tempFolder.CreateTextFile("leap.yaml", """
            services:
              app:
                runners:
                - type: remote
                  environments:
                    dev: https://example.com/
            """);

        context.AddConfigurationFiles(leapYamlPath);
        context.AddHttpHealthCheckUrls(() => context.GetUrl("app"));
        await context.Start();

        // Run "aspire ps --format json" and capture the output
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();
        var result = await CliWrap.Cli.Wrap("aspire")
            .WithArguments(["ps", "--format", "json"])
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cts.Token);

        testOutputHelper.WriteLine($"aspire ps exit code: {result.ExitCode}");
        testOutputHelper.WriteLine($"aspire ps stdout: {stdOut}");
        testOutputHelper.WriteLine($"aspire ps stderr: {stdErr}");

        Assert.Equal(0, result.ExitCode);

        var json = stdOut.ToString().Trim();
        Assert.False(string.IsNullOrEmpty(json), "aspire ps returned empty output");

        // Parse the JSON array output
        var appHosts = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal(JsonValueKind.Array, appHosts.ValueKind);
        Assert.True(appHosts.GetArrayLength() > 0, "aspire ps returned no app hosts");

        var appHostPath = appHosts[0].GetProperty("appHostPath").GetString();
        testOutputHelper.WriteLine($"AppHostPath from aspire ps: {appHostPath}");

        var expectedPath = leapYamlPath.ToString();
        testOutputHelper.WriteLine($"Expected path: {expectedPath}");

        Assert.Equal(expectedPath, appHostPath, ignoreCase: true);
    }
}
