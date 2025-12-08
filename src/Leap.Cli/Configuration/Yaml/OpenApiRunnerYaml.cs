using Leap.Cli.Model.Traits;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class OpenApiRunnerYaml : RunnerYaml, IHasPort
{
    public const string YamlDiscriminator = Constants.OpenApiRunnerYamlDiscriminator;

    [YamlMember(Alias = "spec", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public string? Specification { get; set; }

    [YamlMember(Alias = "port", DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
    public int? Port { get; set; }
}