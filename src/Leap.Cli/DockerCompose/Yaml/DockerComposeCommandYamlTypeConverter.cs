using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposeCommandYamlTypeConverter : IYamlTypeConverter
{
    public static readonly DockerComposeCommandYamlTypeConverter Instance = new();

    private DockerComposeCommandYamlTypeConverter()
    {
    }

    public bool Accepts(Type type)
    {
        return type == typeof(DockerComposeCommandYaml);
    }

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        _ = parser.Consume<SequenceStart>();
        var command = new DockerComposeCommandYaml();

        while (!parser.Accept<SequenceEnd>(out _))
        {
            var scalar = parser.Consume<Scalar>();
            command.Add(scalar.Value);
        }

        parser.MoveNext();
        return command;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var command = (DockerComposeCommandYaml)value!;

        emitter.Emit(new SequenceStart(AnchorName.Empty, TagName.Empty, isImplicit: true, SequenceStyle.Flow));

        foreach (var argument in command)
        {
            emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, argument, ScalarStyle.DoubleQuoted, isPlainImplicit: false, isQuotedImplicit: true));
        }

        emitter.Emit(new SequenceEnd());
    }
}