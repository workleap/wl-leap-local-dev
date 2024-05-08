using Leap.Cli.Aspire;
using NuGet.Versioning;

namespace Leap.Cli.Tests.Aspire;

public class AspireManagerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void ExtractAspireVersionFromAssemblyMetadata_Works()
    {
        Assert.True(
            NuGetVersion.TryParseStrict(AspireManager.ExtractAspireVersionFromAssemblyMetadata(), out var aspireVersion),
            userMessage: "The Aspire NuGet package version should have been available from the assembly metadata.");

        testOutputHelper.WriteLine($"Aspire NuGet package version: {aspireVersion}");
    }
}