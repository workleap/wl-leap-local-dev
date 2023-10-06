namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class KeyValueCollectionYaml : Dictionary<string, string>
{
    public KeyValueCollectionYaml()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }
}