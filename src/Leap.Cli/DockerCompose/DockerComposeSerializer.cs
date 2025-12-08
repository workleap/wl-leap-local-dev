using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose;

internal static class DockerComposeSerializer
{
    private const string UnixLineEnding = "\n";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithTypeConverter(DockerComposePortMappingYamlTypeConverter.Instance)
        .WithTypeConverter(KeyValueCollectionYamlTypeConverter.MapWriter)
        .WithTypeConverter(DockerComposeVolumeMappingYamlTypeConverter.Instance)
        .WithTypeConverter(InlinedQuotedStringCollectionYamlTypeConverter.Instance)
        .WithTypeConverter(DockerComposeImageNameTypeConverter.Instance)
        .IgnoreUnmatchedProperties() // don't throw an exception if there are unknown properties
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithTypeConverter(DockerComposePortMappingYamlTypeConverter.Instance)
        .WithTypeConverter(KeyValueCollectionYamlTypeConverter.MapWriter)
        .WithTypeConverter(DockerComposeVolumeMappingYamlTypeConverter.Instance)
        .WithTypeConverter(InlinedQuotedStringCollectionYamlTypeConverter.Instance)
        .WithTypeConverter(DockerComposeImageNameTypeConverter.Instance)
        .WithNewLine(UnixLineEnding) // keep compatibility with Linux and macOS
        .DisableAliases() // don't use anchors and aliases (references to identical objects)
        .Build();

    public static async Task<DockerComposeYaml> DeserializeAsync(Stream stream, CancellationToken cancellationToken)
    {
        // It's the responsibility of the caller to dispose the stream
        string dockerComposeYamlContents;
        using (var reader = new StreamReader(stream, leaveOpen: true))
        {
            dockerComposeYamlContents = await reader.ReadToEndAsync(cancellationToken);
        }

        // YamlDotNet doesn't support asynchroneous serialization, and we rather access the file system asynchronously
        return Deserializer.Deserialize<DockerComposeYaml>(dockerComposeYamlContents);
    }

    public static async Task SerializeAsync(Stream stream, DockerComposeYaml dockerComposeYaml, CancellationToken cancellationToken)
    {
        // YamlDotNet doesn't support asynchroneous serialization, and we rather access the file system asynchronously
        var dockerComposeYamlContents = Serializer.Serialize(dockerComposeYaml);

        // It's the responsibility of the caller to dispose the stream
        await using var writer = new StreamWriter(stream, leaveOpen: true);
        await writer.WriteAsync(dockerComposeYamlContents.AsMemory(), cancellationToken);
    }
}