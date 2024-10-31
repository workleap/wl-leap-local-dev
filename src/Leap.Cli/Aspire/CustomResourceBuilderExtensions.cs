namespace Leap.Cli.Aspire;

internal static class CustomResourceBuilderExtensions
{
    // The default WaitFor implementation requires a resource object. However, IResource only contains name and annotation properties.
    // Thus, this extension method helps simplify the code by just requiring a name.
    // https://github.com/dotnet/aspire/blob/v9.0.0-rc.1.24511.1/src/Aspire.Hosting/ResourceBuilderExtensions.cs#L599
    public static IResourceBuilder<T> WaitFor<T>(this IResourceBuilder<T> builder, string[] dependencyNames) where T : IResourceWithWaitSupport
    {
        foreach (var dependencyName in dependencyNames)
        {
            builder.WithAnnotation(new WaitAnnotation(new DependencyResource(dependencyName), WaitType.WaitUntilHealthy));
        }

        return builder;
    }

    private sealed class DependencyResource(string name) : Resource(name), IResourceWithWaitSupport;
}