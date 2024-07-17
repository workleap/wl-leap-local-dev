using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Yaml;
using YamlDotNet.Serialization;

namespace Leap.Cli.Configuration;

internal static class LeapYamlSerializer
{
    private const string UnixLineEnding = "\n";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithTypeConverter(KeyValueCollectionYamlTypeConverter.MapWriter)
        .WithTypeConverter(DockerRunnerVolumeMappingYamlTypeConverter.Instance)
        .IgnoreUnmatchedProperties() // don't throw an exception if there are unknown properties
        .WithTypeDiscriminatingNodeDeserializer(options =>
        {
            options.AddKeyValueTypeDiscriminator<RunnerYaml>("type", new Dictionary<string, Type>
            {
                [ExecutableRunnerYaml.YamlDiscriminator] = typeof(ExecutableRunnerYaml),
                [DockerRunnerYaml.YamlDiscriminator] = typeof(DockerRunnerYaml),
                [DotnetRunnerYaml.YamlDiscriminator] = typeof(DotnetRunnerYaml),
                [OpenApiRunnerYaml.YamlDiscriminator] = typeof(OpenApiRunnerYaml),
                [RemoteRunnerYaml.YamlDiscriminator] = typeof(RemoteRunnerYaml),
            });
        })
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithTypeConverter(KeyValueCollectionYamlTypeConverter.MapWriter)
        .WithTypeConverter(DockerRunnerVolumeMappingYamlTypeConverter.Instance)
        .WithNewLine(UnixLineEnding) // keep compatibility with Linux and macOS
        .DisableAliases() // don't use anchors and aliases (references to identical objects)
        .Build();

    public static async Task<LeapYaml?> DeserializeAsync(Stream stream, CancellationToken cancellationToken)
    {
        // It's the responsibility of the caller to dispose the stream
        string leapYamlContents;
        using (var reader = new StreamReader(stream, leaveOpen: true))
        {
            leapYamlContents = await reader.ReadToEndAsync(cancellationToken);
        }

        return Deserializer.Deserialize<LeapYaml>(leapYamlContents);
    }

    public static async Task SerializeAsync(Stream stream, LeapYaml leapYaml, CancellationToken cancellationToken)
    {
        var leapYamlContents = Serializer.Serialize(leapYaml);

        // It's the responsibility of the caller to dispose the stream
        await using var writer = new StreamWriter(stream, leaveOpen: true);
        await writer.WriteAsync(leapYamlContents.AsMemory(), cancellationToken);
    }
}