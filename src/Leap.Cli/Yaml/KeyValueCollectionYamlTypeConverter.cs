using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Leap.Cli.Yaml;

internal sealed class KeyValueCollectionYamlTypeConverter : IYamlTypeConverter
{
    public static readonly KeyValueCollectionYamlTypeConverter Instance = new();

    private static readonly Regex KeyValueRegex = new Regex("^(?<name>[^=]+)=(?<value>.*)$", RegexOptions.Compiled);
    private static readonly char[] KeyCharactersThatRequireQuotes = { ' ', '/', '\\', '~', ':', '$', '{', '}' };

    private KeyValueCollectionYamlTypeConverter()
    {
    }

    public bool Accepts(Type type)
    {
        return type == typeof(KeyValueCollectionYaml);
    }

    public object ReadYaml(IParser parser, Type type)
    {
        if (parser.TryConsume<MappingStart>(out _))
        {
            return ParseMapping(parser);
        }

        _ = parser.Consume<SequenceStart>();
        return ParseSequence(parser);
    }

    private static KeyValueCollectionYaml ParseMapping(IParser parser)
    {
        var keyValueCollection = new KeyValueCollectionYaml();

        while (!parser.Accept<MappingEnd>())
        {
            var name = parser.Consume<Scalar>();
            var value = parser.Consume<Scalar>();
            keyValueCollection[name.Value] = value.Value;
        }

        parser.MoveNext();
        return keyValueCollection;
    }

    private static KeyValueCollectionYaml ParseSequence(IParser parser)
    {
        var keyValueCollection = new KeyValueCollectionYaml();

        while (!parser.Accept<SequenceEnd>())
        {
            var scalar = parser.Consume<Scalar>();

            if (KeyValueRegex.Match(scalar.Value) is { Success: true } match)
            {
                var name = match.Groups["name"].Value;
                var value = match.Groups["value"].Value;
                keyValueCollection[name] = value;
            }
            else
            {
                throw new InvalidOperationException("Invalid key value mapping: " + scalar.Value);
            }
        }

        parser.MoveNext();
        return keyValueCollection;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        var entries = (KeyValueCollectionYaml)value!;

        emitter.Emit(new MappingStart(AnchorName.Empty, TagName.Empty, isImplicit: true, MappingStyle.Block));

        foreach (var entry in entries)
        {
            var keyScalar = entry.Key.IndexOfAny(KeyCharactersThatRequireQuotes) >= 0
                ? new Scalar(AnchorName.Empty, TagName.Empty, entry.Key, ScalarStyle.DoubleQuoted, isPlainImplicit: false, isQuotedImplicit: true)
                : new Scalar(AnchorName.Empty, TagName.Empty, entry.Key, ScalarStyle.Plain, isPlainImplicit: true, isQuotedImplicit: false);

            emitter.Emit(keyScalar);
            emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, entry.Value, ScalarStyle.DoubleQuoted, isPlainImplicit: false, isQuotedImplicit: true));
        }

        emitter.Emit(new MappingEnd());
    }
}