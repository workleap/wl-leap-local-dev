using System.IO.Abstractions;
using System.Text.Json.Nodes;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class ConfigureOpenTelemetryPipelineStep : IPipelineStep
{
    // We export another port on the host to avoid conflicts with any existing Zipkin (ex: Dapr CLI)
    private const int HostZipkinPort = 9412;
    private const int ContainerZipkinPort = 9411; // Zipkin default port
    private const string ZipkinServiceName = "zipkin";
    private const string ZipkinContainerName = "leap-zipkin";

    // We export another port on the host to avoid conflicts with any existing OTLP Collector
    private const int HostOtlpCollectorPort = 16285;
    private const int ContainerOtlpCollectorPort = 4318; // OTLP Collector default port on HTTP
    private const string OtlpCollectorServiceName = "otlp-collector";
    private const string OtlpCollectorContainerName = "leap-otlp-collector";

    // Using "host.docker.internal" allows container not to be on the same leap network
    private static readonly string HostOtlpCollectorEndpoint = $"http://127.0.0.1:{HostOtlpCollectorPort}";
    private static readonly string ContainerOtlpCollectorEndpoint = $"http://host.docker.internal:{HostOtlpCollectorPort}";

    private static readonly string ZipkinUrl = $"http://127.0.0.1:{HostZipkinPort}";

    private static readonly string OtlpCollectorYamlFilePath = Path.Combine(Constants.GeneratedDirectoryPath, "otel-collector.yaml");
    private static readonly string OtlpCollectorYamlFileContents = @$"
receivers:
  otlp:
    protocols:
      http:

exporters:
  debug:
  zipkin:
    endpoint: ""http://{ZipkinServiceName}:{ContainerZipkinPort}/api/v2/spans""
    format: proto

processors:
  batch:

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [batch]
      exporters: [debug, zipkin]
    metrics:
      receivers: [otlp]
      processors: [batch]
      exporters: [debug]
".Trim();

    private readonly IConfigureDockerCompose _dockerCompose;
    private readonly IConfigureEnvironmentVariables _environmentVariables;
    private readonly IConfigureAppSettingsJson _appSettingsJson;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;

    public ConfigureOpenTelemetryPipelineStep(
        IConfigureDockerCompose dockerCompose,
        IConfigureEnvironmentVariables environmentVariables,
        IConfigureAppSettingsJson appSettingsJson,
        IFileSystem fileSystem,
        ILogger<ConfigureOpenTelemetryPipelineStep> logger)
    {
        this._dockerCompose = dockerCompose;
        this._environmentVariables = environmentVariables;
        this._appSettingsJson = appSettingsJson;
        this._fileSystem = fileSystem;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (state.Services.Count == 0)
        {
            return;
        }

        // TODO IO error handling
        await this._fileSystem.File.WriteAllTextAsync(OtlpCollectorYamlFilePath, OtlpCollectorYamlFileContents, cancellationToken);

        ConfigureDockerCompose(this._dockerCompose.Configuration);
        this._environmentVariables.Configure(ConfigureEnvironmentVariables);
        ConfigureAppSettingsJson(this._appSettingsJson.Configuration);

        this._logger.LogInformation("OTLP server running at: {OtlpEndpoint}", HostOtlpCollectorEndpoint);
        this._logger.LogInformation("Browse distributed traces with Zipkin at: {ZipkinUrl}", ZipkinUrl);
    }

    private static void ConfigureDockerCompose(DockerComposeYaml dockerComposeYaml)
    {
        var zipkin = new DockerComposeServiceYaml
        {
            Image = "openzipkin/zipkin:latest",
            ContainerName = ZipkinContainerName,
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Ports = { new DockerComposePortMappingYaml(HostZipkinPort, ContainerZipkinPort) },
        };

        var otelCollector = new DockerComposeServiceYaml
        {
            DependsOn = new List<string> { ZipkinServiceName },
            Image = "otel/opentelemetry-collector:latest",
            ContainerName = OtlpCollectorContainerName,
            Command = new DockerComposeCommandYaml
            {
                "--config=/etc/otel-collector.yaml",
            },
            Restart = DockerComposeConstants.Restart.UnlessStopped,
            Ports = { new DockerComposePortMappingYaml(HostOtlpCollectorPort, ContainerOtlpCollectorPort) },
            Volumes =
            {
                new DockerComposeVolumeMappingYaml(OtlpCollectorYamlFilePath, "/etc/otel-collector.yaml"),
            },
        };

        dockerComposeYaml.Services[ZipkinServiceName] = zipkin;
        dockerComposeYaml.Services[OtlpCollectorServiceName] = otelCollector;
    }

    private static void ConfigureEnvironmentVariables(List<EnvironmentVariable> environmentVariables)
    {
        // Do we want to add the environment variables after we verified that the instance is ready?
        environmentVariables.AddRange(new[]
        {
            new EnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", HostOtlpCollectorEndpoint, EnvironmentVariableScope.Host),
            new EnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", ContainerOtlpCollectorEndpoint, EnvironmentVariableScope.Container),
        });
    }

    private static void ConfigureAppSettingsJson(JsonObject appsettings)
    {
        appsettings["OTEL_EXPORTER_OTLP_ENDPOINT"] = HostOtlpCollectorEndpoint;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}