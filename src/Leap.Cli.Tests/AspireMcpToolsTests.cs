#pragma warning disable CA1849 // Call async methods when in an async method
using System.Text;
using System.Text.Json;
using CliWrap;
using Meziantou.Extensions.Logging.Xunit;
using Meziantou.Framework;
using Microsoft.Extensions.Logging;
using Workleap.Leap.Testing;

namespace Leap.Cli.Tests;

[Collection("IntegrationTests")]
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
        var leapYamlPath = tempFolder.CreateTextFile("leap.yaml", """
            dependencies:
            - type: postgres
              mcp: true
            """);

        context.AddConfigurationFiles(leapYamlPath);
        await context.Start();

        await this.AssertMcpToolsAvailable(leapYamlPath, cts.Token);
    }

    [Fact]
    public async Task MongoMcp_ShouldExposeToolsViaAspireMcp()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await using var context = this.CreateContext(cancellationToken: cts.Token);

        await using var tempFolder = TemporaryDirectory.Create();
        var leapYamlPath = tempFolder.CreateTextFile("leap.yaml", """
            dependencies:
            - type: mongo
              mcp: true
            """);

        context.AddConfigurationFiles(leapYamlPath);
        await context.Start();

        await this.AssertMcpToolsAvailable(leapYamlPath, cts.Token);
    }

    private async Task AssertMcpToolsAvailable(FullPath leapYamlPath, CancellationToken cancellationToken)
    {
        // Run "aspire mcp tools --format json" and capture the output.
        // Use --apphost to target the specific app host since the working directory
        // differs from the temp folder where the leap.yaml is located.
        // The MCP container may take a moment to register with the Aspire MCP proxy,
        // so retry until tools are found.
        const int maxAttempts = 10;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();
            var result = await CliWrap.Cli.Wrap("aspire")
                .WithArguments(["mcp", "tools", "--format", "json", "--apphost", leapYamlPath])
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync(cancellationToken);

            testOutputHelper.WriteLine($"[Attempt {attempt}] aspire mcp tools exit code: {result.ExitCode}");
            testOutputHelper.WriteLine($"[Attempt {attempt}] aspire mcp tools stdout: {stdOut}");
            testOutputHelper.WriteLine($"[Attempt {attempt}] aspire mcp tools stderr: {stdErr}");

            // The aspire CLI may write JSON to stdout or stderr depending on the version
            var json = stdOut.ToString().Trim();
            if (string.IsNullOrEmpty(json))
            {
                json = stdErr.ToString().Trim();
            }

            if (result.ExitCode == 0 && json.StartsWith('['))
            {
                var tools = JsonSerializer.Deserialize<JsonElement>(json);
                Assert.Equal(JsonValueKind.Array, tools.ValueKind);
                Assert.True(tools.GetArrayLength() > 0, "aspire mcp tools returned no tools");
                return;
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        Assert.Fail($"aspire mcp tools did not return MCP tools after {maxAttempts} attempts");
    }
}
