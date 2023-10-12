namespace Leap.Cli.Configuration.Yaml;

internal sealed class DependencyYaml : DynamicObjectYaml
{
    public string Type
    {
        get => base.GetScalar("type") ?? string.Empty;
        set => base["type"] = value;
    }
}