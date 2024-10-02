using Leap.Cli.Yaml;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class InlinedQuotedStringCollectionYamlTypeConverter : IYamlTypeConverter
{
    public static readonly InlinedQuotedStringCollectionYamlTypeConverter Instance = new();

    private InlinedQuotedStringCollectionYamlTypeConverter()
    {
    }

    public bool Accepts(Type type)
    {
        return type == typeof(InlinedQuotedStringCollectionYaml);
    }

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        _ = parser.Consume<SequenceStart>();
        var items = new InlinedQuotedStringCollectionYaml();

        while (!parser.Accept<SequenceEnd>(out _))
        {
            var scalar = parser.Consume<Scalar>();
            items.Add(scalar.Value);
        }

        parser.MoveNext();
        return items;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var items = (InlinedQuotedStringCollectionYaml)value!;

        emitter.Emit(new SequenceStart(AnchorName.Empty, TagName.Empty, isImplicit: true, SequenceStyle.Flow));

        foreach (var item in items)
        {
            emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, item, ScalarStyle.DoubleQuoted, isPlainImplicit: false, isQuotedImplicit: true));
        }

        emitter.Emit(new SequenceEnd());
    }
}