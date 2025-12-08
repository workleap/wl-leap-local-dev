using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed partial class DockerComposeVolumeMappingYamlTypeConverter : IYamlTypeConverter
{
    public static readonly DockerComposeVolumeMappingYamlTypeConverter Instance = new();

    private DockerComposeVolumeMappingYamlTypeConverter()
    {
    }

    public bool Accepts(Type type)
    {
        return type == typeof(DockerComposeVolumeMappingYaml);
    }

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();

        if (VolumeMappingRegex().Match(scalar.Value) is { Success: true } match)
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

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var mapping = (DockerComposeVolumeMappingYaml)value!;
        emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, $"{mapping.Source}:{mapping.Destination}:{mapping.Mode}", ScalarStyle.DoubleQuoted, isPlainImplicit: true, isQuotedImplicit: true));
    }

    [GeneratedRegex("^(?<src>[^:]+):(?<dst>[^:]+)(:(?<mode>(ro|rw)))?$", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex VolumeMappingRegex();
}