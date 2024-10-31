using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeImageNameTypeConverter : IYamlTypeConverter
{
    public static readonly DockerComposeImageNameTypeConverter Instance = new();

    private DockerComposeImageNameTypeConverter()
    {
    }

    public bool Accepts(Type type)
    {
        return type == typeof(DockerComposeImageName);
    }

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        return new DockerComposeImageName(scalar.Value);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var imageName = (DockerComposeImageName)value!;
        emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, imageName.Value, ScalarStyle.DoubleQuoted, isPlainImplicit: false, isQuotedImplicit: true));
    }
}