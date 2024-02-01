using Leap.Cli.ProcessCompose.Yaml;
using Leap.Cli.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.ProcessCompose;

internal static class ProcessComposeSerializer
{
    private const string UnixLineEnding = "\n";

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithTypeConverter(KeyValueCollectionYamlTypeConverter.SequenceWriter)
        .WithNewLine(UnixLineEnding) // keep compatibility with Linux and macOS
        .DisableAliases() // don't use anchors and aliases (references to identical objects)
        .Build();

    public static async Task SerializeAsync(Stream stream, ProcessComposeYaml processComposeYaml, CancellationToken cancellationToken)
    {
        // YamlDotNet doesn't support asynchroneous serialization, and we rather access the file system asynchronously
        var processComposeYamlContents = Serializer.Serialize(processComposeYaml);

        // It's the responsibility of the caller to dispose the stream
        await using var writer = new StreamWriter(stream, leaveOpen: true);
        await writer.WriteAsync(processComposeYamlContents.AsMemory(), cancellationToken);
    }
}