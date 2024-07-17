using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration.Yaml;

internal sealed partial class DockerRunnerVolumeMappingYamlTypeConverter : IYamlTypeConverter
{
    public static readonly DockerRunnerVolumeMappingYamlTypeConverter Instance = new();

    private DockerRunnerVolumeMappingYamlTypeConverter()
    {
    }

    public bool Accepts(Type type)
    {
        return type == typeof(DockerRunnerVolumeMappingYaml);
    }

    public object ReadYaml(IParser parser, Type type)
    {
        var scalar = parser.Consume<Scalar>();

        if (VolumeMappingRegex().Match(scalar.Value) is { Success: true } match)
        {
            return new DockerRunnerVolumeMappingYaml
            {
                Source = match.Groups["src"].Value,
                Destination = match.Groups["dst"].Value,
            };
        }

        throw new InvalidOperationException("Invalid volume mapping: " + scalar.Value);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        var mapping = (DockerRunnerVolumeMappingYaml)value!;
        emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, $"{mapping.Source}:{mapping.Destination}", ScalarStyle.DoubleQuoted, isPlainImplicit: true, isQuotedImplicit: true));
    }

    [GeneratedRegex("^(?<src>[^:]+):(?<dst>[^:]+)$", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex VolumeMappingRegex();
}