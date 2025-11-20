using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;

namespace Leap.Cli.Aspire;

internal sealed class EnableAspireDashboardDiagnosticLoggingLifecycleHook : IDistributedApplicationEventingSubscriber
{
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>(this.BeforeStartAsync);
        return Task.CompletedTask;
    }

    private Task BeforeStartAsync(BeforeStartEvent @event, CancellationToken cancellationToken = default)
    {
        // See https://github.com/dotnet/aspire/blob/v8.0.0-preview.7.24251.11/src/Aspire.Hosting/Dashboard/DashboardLifecycleHook.cs
        // which takes care of adding the dashboard as an executable resource and forwards the dashboard logs to the app host
        var dashboardResource = @event.Model.Resources.Single(x => x.Name == "aspire-dashboard");

        dashboardResource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["Logging__LogLevel__Default"] = "Trace";
            context.EnvironmentVariables["Logging__LogLevel__Grpc"] = "Information";
            context.EnvironmentVariables["Logging__LogLevel__Microsoft.Extensions.Localization"] = "Information";
        }));

        return Task.CompletedTask;
    }
}