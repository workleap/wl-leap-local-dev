using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;

namespace Leap.Cli.Aspire;

internal sealed class UseLeapCertificateForAspireDashboardLifecycleHook : IDistributedApplicationEventingSubscriber
{
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>(this.BeforeStartAsync);
        return Task.CompletedTask;
    }

    private Task BeforeStartAsync(BeforeStartEvent @event, CancellationToken cancellationToken = default)
    {
        var dashboardResource = @event.Model.Resources.Single(x => x.Name == "aspire-dashboard");

        dashboardResource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["Kestrel__Certificates__Default__Path"] = Constants.LocalCertificateCrtFilePath;
            context.EnvironmentVariables["Kestrel__Certificates__Default__KeyPath"] = Constants.LocalCertificateKeyFilePath;
        }));

        return Task.CompletedTask;
    }
}