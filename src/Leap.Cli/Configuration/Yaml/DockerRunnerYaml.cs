using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class DockerRunnerYaml : RunnerYaml
{
    public const string YamlDiscriminator = "docker";

    [YamlMember(Alias = "image", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Image { get; set; }

    [YamlMember(Alias = "containerPort", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public int? ContainerPort { get; set; }

    [YamlMember(Alias = "hostPort", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public int? HostPort { get; set; }

    [YamlMember(Alias = "protocol", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Protocol { get; set; }
}