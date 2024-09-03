using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Leap.Cli.Yaml;

internal sealed class KeyValueCollectionYamlTypeConverter : IYamlTypeConverter
{
    public static readonly KeyValueCollectionYamlTypeConverter MapWriter = new(WriteFormat.Map);
    public static readonly KeyValueCollectionYamlTypeConverter SequenceWriter = new(WriteFormat.Sequence);

    private static readonly Regex KeyValueRegex = new Regex("^(?<name>[^=]+)=(?<value>.*)$", RegexOptions.Compiled);
    private static readonly char[] KeyCharactersThatRequireQuotes = { ' ', '/', '\\', '~', ':', '$', '{', '}' };

    private readonly WriteFormat _writeFormat;

    private KeyValueCollectionYamlTypeConverter(WriteFormat writeFormat)
    {
        this._writeFormat = writeFormat;
    }

    private enum WriteFormat
    {
        Sequence,
        Map,
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

        while (!parser.Accept<MappingEnd>(out _))
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

        while (!parser.Accept<SequenceEnd>(out _))
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

        if (this._writeFormat == WriteFormat.Map)
        {
            WriteAsMap(emitter, entries);
        }
        else if (this._writeFormat == WriteFormat.Sequence)
        {
            WriteAsSequence(emitter, entries);
        }
    }

    private static void WriteAsMap(IEmitter emitter, KeyValueCollectionYaml entries)
    {
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

    private static void WriteAsSequence(IEmitter emitter, KeyValueCollectionYaml entries)
    {
        emitter.Emit(new SequenceStart(AnchorName.Empty, TagName.Empty, isImplicit: true, SequenceStyle.Block));

        foreach (var (key, value) in entries)
        {
            emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, $"{key}={value}", ScalarStyle.DoubleQuoted, isPlainImplicit: false, isQuotedImplicit: true));
        }

        emitter.Emit(new SequenceEnd());
    }
}