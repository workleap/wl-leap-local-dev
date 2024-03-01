using Leap.Cli.Aspire;
using Leap.Cli.Extensions;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class PrepareBindingsPipelineStep : IPipelineStep
{
    private readonly IFeatureManager _featureManager;
    private readonly ILogger<PrepareBindingsPipelineStep> _logger;
    private readonly IAspireManager _aspire;
    private readonly IConfigureEnvironmentVariables _environmentVariables;
    private readonly IPortManager _portManager;

    public PrepareBindingsPipelineStep(
        IFeatureManager featureManager,
        ILogger<PrepareBindingsPipelineStep> logger,
        IAspireManager aspire,
        IConfigureEnvironmentVariables environmentVariables,
        IPortManager portManager)
    {
        this._featureManager = featureManager;
        this._logger = logger;
        this._aspire = aspire;
        this._environmentVariables = environmentVariables;
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

        // .NET specific environment variables. Not harmful for non-.NET services.
        // .NET runners can still override these values in their own environment variables.
        service.EnvironmentVariables.TryAdd("DOTNET_ENVIRONMENT", "Development");

        // Hide OpenTelemetry traffic from .NET apps logs (it's noisy)
        service.EnvironmentVariables.TryAdd("Logging__LogLevel__System.Net.Http.HttpClient.OtlpMetricExporter", "Warning");
        service.EnvironmentVariables.TryAdd("Logging__LogLevel__System.Net.Http.HttpClient.OtlpTraceExporter", "Warning");

        // Based on this internal .NET Aspire method that we can't directly use (reserved to ".AddProject" which we can't use)
        // https://github.com/dotnet/aspire/blob/cdcc995aac7b220351868c40ad2d7c6b66b6c7c2/src/Aspire.Hosting/Extensions/ProjectResourceBuilderExtensions.cs#L53-L56
        service.EnvironmentVariables.TryAdd("OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EXCEPTION_LOG_ATTRIBUTES", "true");
        service.EnvironmentVariables.TryAdd("OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES", "true");

        // https://github.com/dotnet/aspire/blob/cdcc995aac7b220351868c40ad2d7c6b66b6c7c2/src/Aspire.Hosting/Dashboard/ConsoleLogsConfigurationExtensions.cs
        service.EnvironmentVariables["DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION"] = "true";
        service.EnvironmentVariables["LOGGING__CONSOLE__FORMATTERNAME"] = "simple";
        service.EnvironmentVariables["LOGGING__CONSOLE__FORMATTEROPTIONS__TIMESTAMPFORMAT"] = "yyyy-MM-ddTHH:mm:ss.fffffff ";

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
        // TODO shall we sanitize the name of the service? Get inspiration from Dapr
        this._aspire.Builder
            .AddExecutable(service.Name, exeBinding.Command, exeBinding.WorkingDirectory, exeBinding.Arguments)
            .WithEndpoint(scheme: "http", hostPort: service.Ingress.InternalPort)
            .WithEnvironment(service.EnvironmentVariables)
            .WithOtlpExporter();
    }

    private void HandleDockerBinding(Service service, DockerBinding dockerBinding)
    {
        // TODO shall we sanitize the name of the service?
        // Tag is set to null to prevent Aspire from adding latest (tag is already included in our image property)
        this._aspire.Builder.AddContainer(service.Name, dockerBinding.Image, tag: string.Empty)

            // TODO tester docker containers avec aspire (networking)
            .WithEndpoint(scheme: "http", hostPort: service.Ingress.InternalPort, containerPort: dockerBinding.ContainerPort)
            .WithEnvironment(service.EnvironmentVariables)
            .WithOtlpExporter();
    }

    private void HandleCsprojBinding(Service service, CsprojBinding csprojBinding)
    {
        var workingDirectoryPath = Path.GetDirectoryName(csprojBinding.Path);

        string[] dotnetRunArgs = ["run", "--project", csprojBinding.Path, "--no-launch-profile"];

        // TODO shall we sanitize the name of the service? Get inspiration from Dapr
        this._aspire.Builder
            .AddExecutable(service.Name, "dotnet", workingDirectoryPath!, dotnetRunArgs)
            .WithEndpoint(scheme: "http", hostPort: service.Ingress.InternalPort)
            .WithEnvironment(service.EnvironmentVariables)
            .WithOtlpExporter();
    }

    private void HandleOpenApiBinding(Service service, OpenApiBinding openApiBinding)
    {
        // See:
        // https://github.com/stoplightio/prism/blob/v5.5.4/docs/getting-started/01-installation.md#docker
        // https://github.com/stoplightio/prism/blob/v5.5.4/Dockerfile#L69
        const int prismContainerPort = 4010;

        // TODO shall we sanitize the name of the service?
        var container = new ContainerResource(service.Name, entrypoint: "mock --host 0.0.0.0 --dynamic /tmp/swagger.yml");

        this._aspire.Builder.AddResource(container)

            // Tag is set to null to prevent Aspire from adding latest (tag is already included in our image property)
            .WithAnnotation(new ContainerImageAnnotation { Image = "stoplight/prism:5", Tag = string.Empty })
            .WithVolumeMount(openApiBinding.Specification, "/tmp/swagger.yml")

            // TODO tester docker containers avec aspire (networking)
            .WithEndpoint(scheme: "http", hostPort: service.Ingress.InternalPort, containerPort: prismContainerPort);
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
