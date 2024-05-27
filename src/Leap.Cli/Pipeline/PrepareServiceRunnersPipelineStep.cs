using Leap.Cli.Aspire;
using Leap.Cli.Model;
using Leap.Cli.Platform;

namespace Leap.Cli.Pipeline;

internal sealed class PrepareServiceRunnersPipelineStep : IPipelineStep
{
    private readonly IAspireManager _aspire;
    private readonly IConfigureEnvironmentVariables _environmentVariables;
    private readonly IPortManager _portManager;
    private readonly IConfigureAppSettingsJson _appSettingsJson;

    public PrepareServiceRunnersPipelineStep(
        IAspireManager aspire,
        IConfigureEnvironmentVariables environmentVariables,
        IPortManager portManager,
        IConfigureAppSettingsJson appSettingsJson)
    {
        this._aspire = aspire;
        this._environmentVariables = environmentVariables;
        this._portManager = portManager;
        this._appSettingsJson = appSettingsJson;
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
        // TODO very dirty way of populating networking information, to refactor
        service.Ingress.Host ??= "127.0.0.1";
        service.Ingress.ExternalPort ??= Constants.LeapReverseProxyPort;
        service.Ingress.InternalPort ??= service.ActiveRunner?.Port ?? this._portManager.GetRandomAvailablePort(cancellationToken);
        service.Ingress.Path ??= "/";

        // .NET specific environment variables. Not harmful for non-.NET services.
        // .NET runners can still override these values in their own service environment variables.
        if (!service.EnvironmentVariables.ContainsKey("ASPNETCORE_ENVIRONMENT"))
        {
            service.EnvironmentVariables.TryAdd("DOTNET_ENVIRONMENT", "Local");
        }

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

        switch (service.ActiveRunner)
        {
            case ExecutableRunner exeRunner:
                this.HandleExecutableRunner(service, exeRunner);
                break;
            case DockerRunner dockerRunner:
                this.HandleDockerRunner(service, dockerRunner);
                break;
            case DotnetRunner dotnetRunner:
                this.HandleDotnetRunner(service, dotnetRunner);
                break;
            case OpenApiRunner openApiRunner:
                this.HandleOpenApiRunner(service, openApiRunner);
                break;
        }

        var envvarName = "SERVICES__" + service.Name.ToUpperInvariant() + "__BASEURL";

        string hostServiceUrl;
        string containerServiceUrl;

        if (service.ActiveRunner is RemoteRunner remoteRunner)
        {
            hostServiceUrl = containerServiceUrl = remoteRunner.Url;
        }
        else
        {
            hostServiceUrl = $"https://{service.Ingress.Host}:{service.Ingress.ExternalPort}";

            // TODO won't work with custom domain, consider mapping custom hosts in docker-compose.yaml
            // TODO wait, if we map custom hosts, we won't be able to trust the certificate from inside the container
            containerServiceUrl = $"http://host.docker.internal:{service.Ingress.InternalPort}";
        }

        // Declare the service URL to the other services
        this._environmentVariables.Configure(x =>
        {
            x.Add(new EnvironmentVariable(envvarName, hostServiceUrl, EnvironmentVariableScope.Host));
            x.Add(new EnvironmentVariable(envvarName, containerServiceUrl, EnvironmentVariableScope.Container));
        });

        // Declare the service URL to the appsettings.json
        this._appSettingsJson.Configuration["Services:" + service.Name + ":BaseUrl"] = hostServiceUrl;
    }

    private void HandleExecutableRunner(Service service, ExecutableRunner exeRunner)
    {
        // TODO shall we sanitize the name of the service? Get inspiration from Dapr
        this._aspire.Builder
            .AddExecutable(service.Name, exeRunner.Command, exeRunner.WorkingDirectory, exeRunner.Arguments)
            .WithEndpoint(scheme: "http", port: service.Ingress.InternalPort)
            .WithEnvironment(service.EnvironmentVariables)
            .WithOtlpExporter();
    }

    private void HandleDockerRunner(Service service, DockerRunner dockerRunner)
    {
        // TODO shall we sanitize the name of the service?
        // Tag is set to null to prevent Aspire from adding latest (tag is already included in our image property)
        this._aspire.Builder.AddContainer(service.Name, dockerRunner.Image, tag: string.Empty)

            // TODO tester docker containers avec aspire (networking)
            .WithEndpoint(scheme: "http", port: service.Ingress.InternalPort, targetPort: dockerRunner.ContainerPort)
            .WithEnvironment(service.EnvironmentVariables)
            .WithOtlpExporter();
    }

    private void HandleDotnetRunner(Service service, DotnetRunner dotnetRunner)
    {
        var workingDirectoryPath = Path.GetDirectoryName(dotnetRunner.ProjectPath);

        // dotnet watch arguments inspired by .NET Aspire:
        // https://github.com/dotnet/aspire/blob/v8.0.1/src/Aspire.Hosting/Dcp/ApplicationExecutor.cs#L1004-L1022
        string[] dotnetRunArgs = dotnetRunner.Watch
            ? ["watch", "--project", dotnetRunner.ProjectPath, "--no-launch-profile", "--non-interactive", "--no-hot-reload"]
            : ["run", "--project", dotnetRunner.ProjectPath, "--no-launch-profile"];

        // TODO shall we sanitize the name of the service? Get inspiration from Dapr
        this._aspire.Builder
            .AddExecutable(service.Name, "dotnet", workingDirectoryPath!, dotnetRunArgs)
            .WithEndpoint(scheme: "http", port: service.Ingress.InternalPort)
            .WithEnvironment(service.EnvironmentVariables)
            .WithOtlpExporter();
    }

    private void HandleOpenApiRunner(Service service, OpenApiRunner openApiRunner)
    {
        // See:
        // https://github.com/stoplightio/prism/blob/v5.5.4/docs/getting-started/01-installation.md#docker
        // https://github.com/stoplightio/prism/blob/v5.5.4/Dockerfile#L69
        const int prismContainerPort = 4010;

        // TODO shall we sanitize the name of the service?
        var builder = this._aspire.Builder.AddContainer(service.Name, "stoplight/prism", tag: "5")
            .WithEndpoint(scheme: "http", port: service.Ingress.InternalPort, targetPort: prismContainerPort);

        if (openApiRunner.IsUrl)
        {
            builder.WithArgs(["mock", "--host", "0.0.0.0", "--dynamic", openApiRunner.Specification]);
        }
        else
        {
            builder.WithArgs(["mock", "--host", "0.0.0.0", "--dynamic", "/tmp/swagger.yml"])
                .WithBindMount(openApiRunner.Specification, "/tmp/swagger.yml", isReadOnly: true);
        }
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
