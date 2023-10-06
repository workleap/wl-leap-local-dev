using Leap.Cli.DockerCompose.Yaml;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Leap.Cli.Config;

internal sealed class LeapConfigSerializer : ILeapConfigSerializer
{
    private const string UnixLineEnding = "\n";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithTypeConverter(KeyValueCollectionYamlTypeConverter.Instance)
        .IgnoreUnmatchedProperties() // don't throw an exception if there are unknown properties
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .WithTypeConverter(KeyValueCollectionYamlTypeConverter.Instance)
        .WithNewLine(UnixLineEnding) // keep compatibility with Linux and macOS
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitDefaults | DefaultValuesHandling.OmitEmptyCollections)
        .Build();

    public Leap Deserialize(Stream stream)
    {
        // It's the responsibility of the caller to dispose the stream
        using var reader = new StreamReader(stream, leaveOpen: true);
        return Deserializer.Deserialize<Leap>(reader);
    }

    public void Serialize(Stream stream, Leap leap)
    {
        // It's the responsibility of the caller to dispose the stream
        using var writer = new StreamWriter(stream, leaveOpen: true);
        Serializer.Serialize(writer, leap);
    }
}