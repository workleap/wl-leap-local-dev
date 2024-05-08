namespace Leap.Cli.Aspire;

internal static class ResourceBuilderExtensions
{
    public static IResourceBuilder<T> WithEnvironment<T>(this IResourceBuilder<T> builder, IEnumerable<KeyValuePair<string, string>> environmentVariables)
        where T : IResourceWithEnvironment
    {
        return builder.WithAnnotation(new EnvironmentCallbackAnnotation(context =>
        {
            foreach (var (key, value) in environmentVariables)
            {
                context.EnvironmentVariables[key] = value;
            }
        }));
    }
}