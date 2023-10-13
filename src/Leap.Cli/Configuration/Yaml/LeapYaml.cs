using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class LeapYaml
{
    private List<DependencyYaml> _dependencies = new();

    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "dependencies", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public List<DependencyYaml> Dependencies
    {
        get => this._dependencies;
        set => this._dependencies = value ?? new();
    }
}