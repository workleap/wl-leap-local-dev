using Leap.Cli.Model.Traits;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class DockerRunnerYaml : RunnerYaml, IHasProtocol, IHasPort
{
    public const string YamlDiscriminator = "docker";

    [YamlMember(Alias = "image", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? ImageAndTag { get; set; }

    [YamlMember(Alias = "containerPort", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public int? ContainerPort { get; set; }

    [YamlMember(Alias = "hostPort", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public int? HostPort
    {
        get => this.Port;
        set => this.Port = value;
    }

    [YamlMember(Alias = "protocol", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Protocol { get; set; }

    [YamlMember(Alias = "volumes", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public DockerRunnerVolumeMappingYaml?[]? Volumes { get; set; }

    [YamlIgnore]
    public int? Port { get; set; }
}