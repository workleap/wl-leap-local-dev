namespace Leap.Cli.Yaml;

internal sealed class KeyValueCollectionYaml : Dictionary<string, string>
{
    public KeyValueCollectionYaml()
        : base(StringComparer.OrdinalIgnoreCase)
    {
    }
}