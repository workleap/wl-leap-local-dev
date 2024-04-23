using Leap.Cli.Aspire;
using Leap.Cli.Model;
using Microsoft.Extensions.Configuration;

namespace Leap.Cli.Pipeline;

internal sealed class ConfigureAspireDashboard(IAspireManager aspireManager) : IPipelineStep
{
    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        aspireManager.Builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ASPNETCORE_URLS"] = AspireManager.AspireDashboardUrlDefaultValue,
            ["DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"] = AspireManager.AspireDashboardOtlpUrlDefaultValue,
            ["DOTNET_RESOURCE_SERVICE_ENDPOINT_URL"] = AspireManager.AspireResourceServiceEndpointUrl,

            // Disable authentication on the Dashboard
            ["AppHost:BrowserToken"] = "",
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
