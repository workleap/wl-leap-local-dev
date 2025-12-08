using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed class RemoteRunnerYaml : RunnerYaml
{
    public const string YamlDiscriminator = Constants.RemoteRunnerYamlDiscriminator;

    [YamlMember(Alias = "url", ScalarStyle = ScalarStyle.DoubleQuoted)]
    public string? Url { get; set; }

    [YamlMember(Alias = "environments", DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
    public Dictionary<string, string?>? Environments { get; set; }
}