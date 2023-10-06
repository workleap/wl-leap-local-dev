using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeVolumeMappingYamlTypeConverter : IYamlTypeConverter
{
    public static readonly DockerComposeVolumeMappingYamlTypeConverter Instance = new();

    private static readonly Regex PortMappingRegex = new Regex(
        "^(?<src>[^:]+):(?<dst>[^:]+)(:(?<mode>(ro|rw)))?$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private DockerComposeVolumeMappingYamlTypeConverter()
    {
    }

    public bool Accepts(Type type)
    {
        return type == typeof(DockerComposeVolumeMappingYaml);
    }

    public object ReadYaml(IParser parser, Type type)
    {
        var scalar = parser.Consume<Scalar>();

        if (PortMappingRegex.Match(scalar.Value) is { Success: true } match)
        {
            return new DockerComposeVolumeMappingYaml
            {
                Source = match.Groups["src"].Value,
                Destination = match.Groups["dst"].Value,
                Mode = match.Groups["mode"].Success ? match.Groups["mode"].Value : DockerComposeConstants.Volume.ReadWrite,
            };
        }

        throw new InvalidOperationException("Invalid volume mapping: " + scalar.Value);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        var mapping = (DockerComposeVolumeMappingYaml)value!;
        emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, $"{mapping.Source}:{mapping.Destination}:{mapping.Mode}", ScalarStyle.DoubleQuoted, isPlainImplicit: true, isQuotedImplicit: true));
    }
}