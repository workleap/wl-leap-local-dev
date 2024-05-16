using Aspire.Hosting.Lifecycle;

namespace Leap.Cli.Aspire;

internal sealed class UseLeapCertificateForAspireDashboardLifecycleHook : IDistributedApplicationLifecycleHook
{
    public Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        var dashboardResource = appModel.Resources.Single(x => x.Name == "aspire-dashboard");

        dashboardResource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
        {
            context.EnvironmentVariables["Kestrel__Certificates__Default__Path"] = Constants.LocalCertificateCrtFilePath;
            context.EnvironmentVariables["Kestrel__Certificates__Default__KeyPath"] = Constants.LocalCertificateKeyFilePath;
        }));

        return Task.CompletedTask;
    }
}