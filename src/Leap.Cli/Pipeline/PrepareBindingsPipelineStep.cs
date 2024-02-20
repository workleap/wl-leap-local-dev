using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Extensions;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Leap.Cli.ProcessCompose;
using Leap.Cli.ProcessCompose.Yaml;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class PrepareBindingsPipelineStep : IPipelineStep
{
    private readonly IFeatureManager _featureManager;
    private readonly ILogger<PrepareBindingsPipelineStep> _logger;
    private readonly IConfigureProcessCompose _processCompose;
    private readonly IConfigureDockerCompose _dockerCompose;
    private readonly IConfigureEnvironmentVariables _environmentVariables;
    private readonly IPrismManager _prismManager;
    private readonly IPortManager _portManager;

    public PrepareBindingsPipelineStep(
        IFeatureManager featureManager,
        ILogger<PrepareBindingsPipelineStep> logger,
        IConfigureProcessCompose processCompose,
        IConfigureDockerCompose dockerCompose,
        IConfigureEnvironmentVariables environmentVariables,
        IPrismManager prismManager,
        IPortManager portManager)
    {
        this._featureManager = featureManager;
        this._logger = logger;
        this._processCompose = processCompose;
        this._dockerCompose = dockerCompose;
        this._environmentVariables = environmentVariables;
        this._prismManager = prismManager;
        this._portManager = portManager;
    }

    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        foreach (var service in state.Services.Values)
        {
            this.PrepareService(service, cancellationToken);
        }

        return Task.CompletedTask;
    }

    private void PrepareService(Service service, CancellationToken cancellationToken)
    {
        if (!this._featureManager.IsEnabled(FeatureIdentifiers.LeapPhase2))
        {
            this._logger.LogPipelineStepSkipped(nameof(PrepareBindingsPipelineStep), FeatureIdentifiers.LeapPhase2);
            return;
        }

        // TODO very dirty way of populating networking information, to refactor
        service.Ingress.Host ??= "127.0.0.1";
        service.Ingress.ExternalPort ??= Constants.LeapReverseProxyPort;
        service.Ingress.InternalPort ??= service.ActiveBinding?.Port ?? this._portManager.GetRandomAvailablePort(cancellationToken);
        service.Ingress.Path ??= "/";

        // Networking-related environment variables for services that need to know their URL
        var advertisedPort = service.ActiveBinding is DockerBinding db ? db.ContainerPort : service.Ingress.InternalPort;
        service.EnvironmentVariables.TryAdd("PORT", advertisedPort.ToString()!);

        // .NET specific environment variables. Not harmful for non-.NET services.
        service.EnvironmentVariables.TryAdd("ASPNETCORE_URLS", $"http://+:{advertisedPort}");
        service.EnvironmentVariables.TryAdd("DOTNET_ENVIRONMENT", "Development");

        // OpenTelemetry
        service.EnvironmentVariables.TryAdd("OTEL_SERVICE_NAME", service.Name);
        service.EnvironmentVariables.TryAdd("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");

        // Hide OpenTelemetry traffic from .NET apps logs
        service.EnvironmentVariables.TryAdd("Logging__LogLevel__System.Net.Http.HttpClient.OtlpMetricExporter", "Warning");
        service.EnvironmentVariables.TryAdd("Logging__LogLevel__System.Net.Http.HttpClient.OtlpTraceExporter", "Warning");

        // Export more information to Zipkin. Based on .NET Aspire:
        // https://github.com/dotnet/aspire/blob/cdcc995aac7b220351868c40ad2d7c6b66b6c7c2/src/Aspire.Hosting/Extensions/ProjectResourceBuilderExtensions.cs#L53-L56
        service.EnvironmentVariables.TryAdd("OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EXCEPTION_LOG_ATTRIBUTES", "true");
        service.EnvironmentVariables.TryAdd("OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES", "true");

        // TODO connecting dependencies to services and services each other
        switch (service.ActiveBinding)
        {
            case ExecutableBinding exeBinding:
                this.HandleExecutableBinding(service, exeBinding);
                break;
            case DockerBinding dockerBinding:
                this.HandleDockerBinding(service, dockerBinding);
                break;
            case CsprojBinding csprojBinding:
                this.HandleCsprojBinding(service, csprojBinding);
                break;
            case OpenApiBinding openApiBinding:
                this.HandleOpenApiBinding(service, openApiBinding);
                break;
        }

        // Declare the service URL to the other services
        this._environmentVariables.Configure(x =>
        {
            var envvarName = "SERVICE__" + service.Name.ToUpperInvariant() + "__URL";

            string hostServiceUrl;
            string containerServiceUrl;

            if (service.ActiveBinding is RemoteBinding remoteBinding)
            {
                hostServiceUrl = containerServiceUrl = remoteBinding.Url;
            }
            else
            {
                hostServiceUrl = $"https://{service.Ingress.Host}:{service.Ingress.ExternalPort}";

                // TODO won't work with custom domain, consider mapping custom hosts in docker-compose.yaml
                // TODO wait, if we map custom hosts, we won't be able to trust the certificate from inside the container
                containerServiceUrl = $"http://host.docker.internal:{service.Ingress.InternalPort}";
            }

            x.Add(new EnvironmentVariable(envvarName, hostServiceUrl, EnvironmentVariableScope.Host));
            x.Add(new EnvironmentVariable(envvarName, containerServiceUrl, EnvironmentVariableScope.Container));
        });
    }

    private void HandleExecutableBinding(Service service, ExecutableBinding exeBinding)
    {
        var commandParts = new List<string>
        {
            ProcessArgument.Escape(exeBinding.Command),
        };

        commandParts.AddRange(exeBinding.Arguments.Select(ProcessArgument.Escape));

        // TODO shall we sanitize the name of the service? Get inspiration from Dapr
        var process = new ProcessComposeProcessYaml
        {
            Command = string.Join(' ', commandParts),
            WorkingDirectory = exeBinding.WorkingDirectory,
        };

        foreach (var (name, value) in service.EnvironmentVariables)
        {
            process.Environment[name] = value;
        }

        this._processCompose.Configuration.Processes[service.Name] = process;
    }

    private void HandleDockerBinding(Service service, DockerBinding dockerBinding)
    {
        // TODO shall we sanitize the name of the service?
        var dockerService = new DockerComposeServiceYaml
        {
            Image = dockerBinding.Image,
            Restart = DockerComposeConstants.Restart.No,
        };

        if (service.Ingress.ExternalPort.HasValue)
        {
            dockerService.Ports = new List<DockerComposePortMappingYaml>
            {
                new DockerComposePortMappingYaml(service.Ingress.InternalPort!.Value, dockerBinding.ContainerPort),
            };
        }

        foreach (var (name, value) in service.EnvironmentVariables)
        {
            dockerService.Environment[name] = value;
        }

        this._dockerCompose.Configuration.Services[service.Name] = dockerService;
    }

    private void HandleCsprojBinding(Service service, CsprojBinding csprojBinding)
    {
        var csprojPath = csprojBinding.Path;
        var workingDirectoryPath = Path.GetDirectoryName(csprojPath);

        // TODO shall we sanitize the name of the service?
        var process = new ProcessComposeProcessYaml
        {
            Command = string.Join(' ', "dotnet", "run", "--no-launch-profile", "--project", ProcessArgument.Escape(csprojPath)),
            WorkingDirectory = workingDirectoryPath,
        };

        foreach (var (name, value) in service.EnvironmentVariables)
        {
            process.Environment[name] = value;
        }

        this._processCompose.Configuration.Processes[service.Name] = process;
    }

    private void HandleOpenApiBinding(Service service, OpenApiBinding openApiBinding)
    {
        var specPath = openApiBinding.Specification;
        var workingDirectoryPath = Path.GetDirectoryName(specPath);

        var commandParts = new[]
        {
            this._prismManager.PrismExecutablePath,
            "mock",
            "--port", service.Ingress.InternalPort!.Value.ToString(),
            "--dynamic",
            ProcessArgument.Escape(specPath),
        };

        // TODO shall we sanitize the name of the service?
        var process = new ProcessComposeProcessYaml
        {
            Command = string.Join(' ', commandParts),
            WorkingDirectory = workingDirectoryPath,
        };

        this._processCompose.Configuration.Processes[service.Name] = process;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
