using System.Globalization;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Leap.Cli.DockerCompose.Yaml;

internal sealed class DockerComposePortMappingYamlTypeConverter : IYamlTypeConverter
{
    public static readonly DockerComposePortMappingYamlTypeConverter Instance = new();

    private const string Ip = "[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}\\.[0-9]{1,3}";
    private const string Hostname = "((?!-)[A-Za-z0-9-]{1,63}(?<!-)\\.)+[A-Za-z]{2,6}"; // https://www.geeksforgeeks.org/how-to-validate-a-domain-name-using-regular-expression/
    private const string Port = "[0-9]{2,5}";

    private static readonly Regex PortMappingRegex = new Regex(
        $"^(((?<host>({Ip})|({Hostname})):)?(?<host_port>{Port}):)?(?<container_port>{Port})$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private DockerComposePortMappingYamlTypeConverter()
    {
    }

    public bool Accepts(Type type)
    {
        return type == typeof(DockerComposePortMappingYaml);
    }

    public object ReadYaml(IParser parser, Type type)
    {
        var scalar = parser.Consume<Scalar>();

        if (PortMappingRegex.Match(scalar.Value) is { Success: true } match)
        {
            var host = match.Groups["host"].Value;
            var containerPort = int.Parse(match.Groups["container_port"].Value, NumberStyles.Integer);
            var hostPort = match.Groups["host_port"].Success ? int.Parse(match.Groups["host_port"].Value, NumberStyles.Integer) : containerPort;

            return new DockerComposePortMappingYaml
            {
                Host = host,
                HostPort = hostPort,
                ContainerPort = containerPort,
            };
        }

        throw new InvalidOperationException("Invalid port mapping: " + scalar.Value);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type)
    {
        var mapping = (DockerComposePortMappingYaml)value!;
        var mappingStr = mapping.Host.Length > 0 ? $"{mapping.Host}:{mapping.HostPort}:{mapping.ContainerPort}" : $"{mapping.HostPort}:{mapping.ContainerPort}";
        emitter.Emit(new Scalar(AnchorName.Empty, TagName.Empty, mappingStr, ScalarStyle.DoubleQuoted, isPlainImplicit: true, isQuotedImplicit: true));
    }
}