using Aspire.Hosting.Lifecycle;

namespace Leap.Cli.Aspire;

// This behavior is inspired by the .NET Aspire sample for Node.js that came out with the first preview:
// https://github.com/dotnet/aspire-samples/blob/96bab738a044779d54adf42ed3a2db71eb4b649d/samples/AspireWithNode/AspireWithNode.AppHost/NodeAppAddPortLifecycleHook.cs
//
// Learn more about the .NET Aspire inner-loop networking here:
// https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/networking-overview#how-service-bindings-work
internal sealed class NetworkingEnvironmentVariablesLifecycleHook : IDistributedApplicationLifecycleHook
{
    public Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
    {
        var executables = appModel.Resources.OfType<ExecutableResource>();

        foreach (var exe in executables)
        {
            if (exe.TryGetEndpoints(out var endpoints))
            {
                var endpointsArray = endpoints.ToArray();

                exe.Annotations.Add(CreateAspNetCoreUrlsEnvironmentCallback(endpointsArray, exe));
                exe.Annotations.Add(CreatePortEnvironmentCallback(endpointsArray, exe));
            }
        }

        return Task.CompletedTask;
    }

    private static EnvironmentCallbackAnnotation CreateAspNetCoreUrlsEnvironmentCallback(EndpointAnnotation[] endpoints, IResource exe) => new(env =>
    {
        var multiEndpoints = endpoints.Length > 1;

        var aspnetcoreUrls = string.Join(';', endpoints.Select(endpoint =>
        {
            var serviceName = multiEndpoints ? $"{exe.Name}_{endpoint.Name}" : exe.Name;
            return $"http://*:{{{{- portForServing \"{serviceName}\" -}}}}";
        }));

        // It's OK if non-.NET apps receive this URL, they won't use it
        env["ASPNETCORE_URLS"] = aspnetcoreUrls;
    });

    private static EnvironmentCallbackAnnotation CreatePortEnvironmentCallback(EndpointAnnotation[] endpoints, IResource exe) => new(env =>
    {
        // We only pick the first endpoint for the PORT environment variable
        // In any case, we're in control of assigning the endpoints when building the .NET Aspire distributed application model
        var serviceName = endpoints.Length > 1 ? $"{exe.Name}_{endpoints[0].Name}" : exe.Name;

        env["PORT"] = $"{{{{- portForServing \"{serviceName}\" -}}}}";
    });
}
