using Leap.Cli.Pipeline;

namespace Leap.Cli.Tests;

public class PopulateServicesFromYamlPipelineStepTests
{
    [Theory]
    [InlineData("a.barley.localhost")]
    [InlineData("a.officevibe.localhost")]
    [InlineData("a.sharegate.localhost")]
    [InlineData("a.workleap.localhost")]
    [InlineData("1.officevibe.localhost")]
    [InlineData("1.sharegate.localhost")]
    [InlineData("1.workleap.localhost")]
    [InlineData("a1.officevibe.localhost")]
    [InlineData("a1.sharegate.localhost")]
    [InlineData("a1.workleap.localhost")]
    [InlineData("a-1.officevibe.localhost")]
    [InlineData("a-1.sharegate.localhost")]
    [InlineData("a-1.workleap.localhost")]
    [InlineData("a-1-a.officevibe.localhost")]
    [InlineData("a-1-a.sharegate.localhost")]
    [InlineData("a-1-a.workleap.localhost")]
    [InlineData("my-super-api.workleap.localhost")]
    [InlineData("a.officevibe.com")]
    [InlineData("a.workleap.com")]
    [InlineData("a.officevibe-dev.com")]
    [InlineData("a.workleap-dev.com")]
    [InlineData("a.workleap-local.com")]
    public void Valid_Localhost_Subdomain_Matches_Wildcard_Localhost_Domains_Regex(string host)
    {
        Assert.Matches(PopulateServicesFromYamlPipelineStep.SupportedWildcardLocalhostDomainNamesRegex, host);
    }

    [Theory]
    [InlineData("")]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("a")]
    [InlineData("a.sharegate.com")]
    [InlineData("-.officevibe.localhost")]
    [InlineData("-.sharegate.localhost")]
    [InlineData("-.workleap.localhost")]
    [InlineData("a-.officevibe.localhost")]
    [InlineData("a-.sharegate.localhost")]
    [InlineData("a-.workleap.localhost")]
    [InlineData("-a.officevibe.localhost")]
    [InlineData("-a.sharegate.localhost")]
    [InlineData("-a.workleap.localhost")]
    [InlineData("a--b.officevibe.localhost")]
    [InlineData("a--b.sharegate.localhost")]
    [InlineData("a--b.workleap.localhost")]
    [InlineData("a.b.officevibe.localhost")]
    [InlineData("a.b.sharegate.localhost")]
    [InlineData("a.b.workleap.localhost")]
    [InlineData("a.officevibe-dev.localhost")]
    [InlineData("a.sharegate-dev.localhost")]
    [InlineData("a.workleap-dev.localhost")]
    [InlineData("my-super-api.notworkleap.localhost")]
    [InlineData("a.officevibe.localhost.com")]
    public void Invalid_Localhost_Subdomain_Does_Not_Match_Wildcard_Localhost_Domains_Regex(string host)
    {
        Assert.DoesNotMatch(PopulateServicesFromYamlPipelineStep.SupportedWildcardLocalhostDomainNamesRegex, host);
    }
}