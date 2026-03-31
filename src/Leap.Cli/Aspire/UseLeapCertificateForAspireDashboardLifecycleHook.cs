#pragma warning disable ASPIRECERTIFICATES001 // HttpsCertificateConfigurationCallbackAnnotation is experimental
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

        // Use HttpsCertificateConfigurationCallbackAnnotation so that Aspire does not override our certificate
        // with its own ASP.NET developer certificate. Without this, Aspire 13.2+ detects no existing
        // HttpsCertificateConfigurationCallbackAnnotation and injects one that overwrites Kestrel__Certificates__Default__Path.
        dashboardResource.Annotations.Add(new HttpsCertificateConfigurationCallbackAnnotation(ctx =>
        {
            ctx.EnvironmentVariables["Kestrel__Certificates__Default__Path"] = Constants.LocalCertificateCrtFilePath;
            ctx.EnvironmentVariables["Kestrel__Certificates__Default__KeyPath"] = Constants.LocalCertificateKeyFilePath;
            return Task.CompletedTask;
        }));

        // HttpsCertificateConfigurationCallbackAnnotation does not set actual process environment variables.
        // We also need EnvironmentCallbackAnnotation to ensure the Kestrel certificate paths are passed
        // to the dashboard process as environment variables.
        dashboardResource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["Kestrel__Certificates__Default__Path"] = Constants.LocalCertificateCrtFilePath;
            context.EnvironmentVariables["Kestrel__Certificates__Default__KeyPath"] = Constants.LocalCertificateKeyFilePath;
        }));

        return Task.CompletedTask;
    }
}
