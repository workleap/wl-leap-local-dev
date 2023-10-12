using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration;

internal static class LeapYamlSerializer
{
    private const string UnixLineEnding = "\n";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithTypeConverter(KeyValueCollectionYamlTypeConverter.Instance)
        .IgnoreUnmatchedProperties() // don't throw an exception if there are unknown properties
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithTypeConverter(KeyValueCollectionYamlTypeConverter.Instance)
        .WithNewLine(UnixLineEnding) // keep compatibility with Linux and macOS
        .DisableAliases() // don't use anchors and aliases (references to identical objects)
        .Build();

    public static LeapYaml Deserialize(Stream stream)
    {
        // It's the responsibility of the caller to dispose the stream
        using var reader = new StreamReader(stream, leaveOpen: true);
        return Deserializer.Deserialize<LeapYaml>(reader);
    }

    public static void Serialize(Stream stream, LeapYaml leapYaml)
    {
        // It's the responsibility of the caller to dispose the stream
        using var writer = new StreamWriter(stream, leaveOpen: true);
        Serializer.Serialize(writer, leapYaml);
    }
}