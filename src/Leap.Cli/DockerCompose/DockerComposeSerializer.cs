using Leap.Cli.DockerCompose.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose;

internal sealed class DockerComposeSerializer : IDockerComposeSerializer
{
    private const string UnixLineEnding = "\n";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithTypeConverter(DockerComposePortMappingYamlTypeConverter.Instance)
        .WithTypeConverter(KeyValueCollectionYamlTypeConverter.Instance)
        .WithTypeConverter(DockerComposeVolumeMappingYamlTypeConverter.Instance)
        .IgnoreUnmatchedProperties() // don't throw an exception if there are unknown properties
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithTypeConverter(DockerComposePortMappingYamlTypeConverter.Instance)
        .WithTypeConverter(KeyValueCollectionYamlTypeConverter.Instance)
        .WithTypeConverter(DockerComposeVolumeMappingYamlTypeConverter.Instance)
        .WithNewLine(UnixLineEnding) // keep compatibility with Linux and macOS
        .DisableAliases() // don't use anchors and aliases (references to identical objects)
        .Build();

    public DockerComposeYaml Deserialize(Stream stream)
    {
        // It's the responsibility of the caller to dispose the stream
        using var reader = new StreamReader(stream, leaveOpen: true);
        return Deserializer.Deserialize<DockerComposeYaml>(reader);
    }

    public void Serialize(Stream stream, DockerComposeYaml dockerComposeYaml)
    {
        // It's the responsibility of the caller to dispose the stream
        using var writer = new StreamWriter(stream, leaveOpen: true);
        Serializer.Serialize(writer, dockerComposeYaml);
    }
}