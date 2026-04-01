#pragma warning disable CA1849 // Call async methods when in an async method
using System.Text;
using System.Text.Json;
using CliWrap;
using Meziantou.Extensions.Logging.Xunit;
using Meziantou.Framework;
using Microsoft.Extensions.Logging;
using Workleap.Leap.Testing;

namespace Leap.Cli.Tests;

public sealed class AspireMcpToolsTests(ITestOutputHelper testOutputHelper)
{
    private LeapTestContext CreateContext(CancellationToken cancellationToken = default)
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

        return new LeapTestContext(factory, cancellationToken: cancellationToken)
        {
            KillExistingLeapInstancesOnStart = true,
            KillLeapInstanceOnStop = true,
            StartLeapTimeout = TimeSpan.FromMinutes(3),
            LeapExecutablePath = leapExecutablePath,
        };
    }

    [Fact]
    public async Task PostgresMcp_ShouldExposeToolsViaAspireMcp()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await using var context = this.CreateContext(cancellationToken: cts.Token);

        await using var tempFolder = TemporaryDirectory.Create();
        context.AddConfigurationFiles(tempFolder.CreateTextFile("leap.yaml", """
            dependencies:
            - type: postgres
              mcp: true
            """));

        await context.Start();

        // Run "aspire mcp tools --format json" and capture the output
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();
        var result = await CliWrap.Cli.Wrap("aspire")
            .WithArguments(["mcp", "tools", "--format", "json"])
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(cts.Token);

        testOutputHelper.WriteLine($"aspire mcp tools exit code: {result.ExitCode}");
        testOutputHelper.WriteLine($"aspire mcp tools stdout: {stdOut}");
        testOutputHelper.WriteLine($"aspire mcp tools stderr: {stdErr}");

        Assert.Equal(0, result.ExitCode);

        var json = stdOut.ToString().Trim();
        Assert.False(string.IsNullOrEmpty(json), "aspire mcp tools returned empty output");

        // Parse the JSON output and verify MCP tools are registered
        var tools = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal(JsonValueKind.Array, tools.ValueKind);
        Assert.True(tools.GetArrayLength() > 0, "aspire mcp tools returned no tools");
    }
}
