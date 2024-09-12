using Leap.Cli.Aspire;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Leap.Cli.Yaml;

namespace Leap.Cli.Pipeline;

internal sealed class PrepareServiceRunnersPipelineStep : IPipelineStep
{
    private readonly IAspireManager _aspire;
    private readonly IConfigureEnvironmentVariables _environmentVariables;
    private readonly IPortManager _portManager;
    private readonly IConfigureAppSettingsJson _appSettingsJson;
    private readonly IConfigureDockerCompose _dockerCompose;

    public PrepareServiceRunnersPipelineStep(
        IAspireManager aspire,
        IConfigureEnvironmentVariables environmentVariables,
        IPortManager portManager,
        IConfigureAppSettingsJson appSettingsJson,
        IConfigureDockerCompose dockerCompose)
    {
        this._aspire = aspire;
        this._environmentVariables = environmentVariables;
        this._portManager = portManager;
        this._appSettingsJson = appSettingsJson;
        this._dockerCompose = dockerCompose;
    }

    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        foreach (var service in state.Services.Values)
        {
            this.PrepareService(state, service, cancellationToken);
        }

        return Task.CompletedTask;
    }

    private void PrepareService(ApplicationState state, Service service, CancellationToken cancellationToken)
    {
        var runner = service.ActiveRunner;

        service.Ingress.LocalhostPort = runner.Port ?? this._portManager.GetRandomAvailablePort(cancellationToken);

        // .NET specific environment variables. Not harmful for non-.NET services.
        // .NET runners can still override these values in their own service environment variables.
        if (!service.EnvironmentVariables.ContainsKey("ASPNETCORE_ENVIRONMENT"))
        {
            service.EnvironmentVariables.TryAdd("DOTNET_ENVIRONMENT", "Local");
        }

        // Hide OpenTelemetry traffic from .NET apps logs (it's noisy)
        service.EnvironmentVariables.TryAdd("Logging__LogLevel__System.Net.Http.HttpClient.OtlpMetricExporter", "Warning");
        service.EnvironmentVariables.TryAdd("Logging__LogLevel__System.Net.Http.HttpClient.OtlpTraceExporter", "Warning");

        // .NET-specific local setup for OpenTelemetry
        // Based on this internal .NET Aspire method that we can't directly use (reserved to ".AddProject" which we can't use)
        // https://github.com/dotnet/aspire/blob/v8.0.1/src/Aspire.Hosting/ProjectResourceBuilderExtensions.cs#L184-L196
        service.EnvironmentVariables.TryAdd("OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EXCEPTION_LOG_ATTRIBUTES", "true");
        service.EnvironmentVariables.TryAdd("OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES", "true");
        service.EnvironmentVariables.TryAdd("OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY", "in_memory");
        service.EnvironmentVariables.TryAdd("OTEL_DOTNET_EXPERIMENTAL_ASPNETCORE_DISABLE_URL_QUERY_REDACTION", "true");
        service.EnvironmentVariables.TryAdd("OTEL_DOTNET_EXPERIMENTAL_HTTPCLIENT_DISABLE_URL_QUERY_REDACTION", "true");

        // https://github.com/dotnet/aspire/blob/v8.0.1/src/Aspire.Hosting/Dashboard/ConsoleLogsConfigurationExtensions.cs#L19-L23
        service.EnvironmentVariables["DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION"] = "true";
        service.EnvironmentVariables["LOGGING__CONSOLE__FORMATTERNAME"] = "simple";
        service.EnvironmentVariables["LOGGING__CONSOLE__FORMATTEROPTIONS__TIMESTAMPFORMAT"] = "yyyy-MM-ddTHH:mm:ss.fffffff ";

        switch (runner)
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
                this.HandleOpenApiRunner(state, service, openApiRunner);
                break;
        }

        var serviceUrlEnvVarName = $"Services__{service.Name}__BaseUrl";
        var serviceUrl = service.GetPrimaryUrl();

        // Declare the service URL to the other services
        this._environmentVariables.Configure(x =>
        {
            x.Add(new EnvironmentVariable(serviceUrlEnvVarName, serviceUrl, EnvironmentVariableScope.Host));
            x.Add(new EnvironmentVariable(serviceUrlEnvVarName, serviceUrl, EnvironmentVariableScope.Container));
        });

        // Declare the service URL to the appsettings.json
        this._appSettingsJson.Configuration[$"Services:{service.Name}:BaseUrl"] = serviceUrl;
    }

    private void HandleExecutableRunner(Service service, ExecutableRunner exeRunner)
    {
        // TODO shall we sanitize the name of the service? Get inspiration from Dapr
        this._aspire.Builder
            .AddExecutable(service.Name, exeRunner.Command, exeRunner.WorkingDirectory, exeRunner.Arguments)
            .WithEndpoint(scheme: exeRunner.Protocol, port: service.Ingress.LocalhostPort, isProxied: false, env: "PORT")
            .WithEnvironment("ASPNETCORE_URLS", exeRunner.Protocol + "://*:" + service.Ingress.LocalhostPort)
            .WithEnvironment("NODE_EXTRA_CA_CERTS", Constants.LeapCertificateAuthorityFilePath)
            .WithEnvironment(context =>
            {
                if ("https".Equals(exeRunner.Protocol, StringComparison.OrdinalIgnoreCase))
                {
                    context.EnvironmentVariables["ASPNETCORE_Kestrel__Certificates__Default__Path"] = Constants.LocalCertificateCrtFilePath;
                    context.EnvironmentVariables["ASPNETCORE_Kestrel__Certificates__Default__KeyPath"] = Constants.LocalCertificateKeyFilePath;
                }
            })
            .WithEnvironment(service.GetServiceAndRunnerEnvironmentVariables())
            .WithOtlpExporter();
    }

    private void HandleDockerRunner(Service service, DockerRunner dockerRunner)
    {
        // .NET Aspire has shown to be unstable with a large number of containers at startup, so we use Docker Compose for now
        // It provides long-running (persistant) containers, which is useful for services that need to be up all the time
        var dockerComposeServiceYaml = new DockerComposeServiceYaml
        {
            Image = dockerRunner.ImageAndTag,
            ContainerName = service.ContainerName,
            Ports = [new DockerComposePortMappingYaml(service.Ingress.LocalhostPort, dockerRunner.ContainerPort)],
            Restart = DockerComposeConstants.Restart.No,

            PullPolicy = dockerRunner.ImageAndTag.Contains("azurecr.io/")
                ? DockerComposeConstants.PullPolicy.Always // Developers are expecting to use their latest images
                : DockerComposeConstants.PullPolicy.Missing,

            Environment = new KeyValueCollectionYaml
            {
                ["ASPNETCORE_URLS"] = dockerRunner.Protocol + "://*:" + dockerRunner.ContainerPort,
                ["PORT"] = dockerRunner.ContainerPort.ToString(),

                // Hack-ish: manually provide OpenTelemetry environment variables as we can't rely on .NET Aspire to do it at runtime
                // https://github.com/dotnet/aspire/blob/v8.1.0/src/Aspire.Hosting/OtlpConfigurationExtensions.cs#L28
                ["OTEL_SERVICE_NAME"] = service.Name,
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "grpc",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = HostNameResolver.ReplaceLocalhostWithContainerHost(AspireManager.AspireDashboardOtlpUrlDefaultValue),
                ["OTEL_EXPORTER_OTLP_HEADERS"] = $"x-otlp-api-key={AspireManager.AspireOtlpDefaultApiKey}",
                ["OTEL_BLRP_SCHEDULE_DELAY"] = "1000",
                ["OTEL_BSP_SCHEDULE_DELAY"] = "1000",
                ["OTEL_METRIC_EXPORT_INTERVAL"] = "1000",
                ["OTEL_TRACES_SAMPLER"] = "always_on",
                ["OTEL_METRICS_EXEMPLAR_FILTER"] = "trace_based",
            },
        };

        foreach (var (source, destination) in dockerRunner.Volumes)
        {
            dockerComposeServiceYaml.Volumes.Add(new DockerComposeVolumeMappingYaml(source, destination, DockerComposeConstants.Volume.ReadOnly));
        }

        string[] wellKnownLinuxCertificateAuthorityBundlePaths =
        [
            // Copied from https://github.com/golang/go/blob/go1.23.0/src/crypto/x509/root_linux.go#L11-L16
            "/etc/ssl/certs/ca-certificates.crt", // Debian/Ubuntu/Gentoo etc.
            "/etc/pki/tls/certs/ca-bundle.crt", // Fedora/RHEL 6
            "/etc/ssl/ca-bundle.pem", // OpenSUSE
            "/etc/pki/tls/cacert.pem", // OpenELEC
            "/etc/pki/ca-trust/extracted/pem/tls-ca-bundle.pem", // CentOS/RHEL 7
            "/etc/ssl/cert.pem", // Alpine Linux
        ];

        // Replace the default container CA bundle with ours
        foreach (var path in wellKnownLinuxCertificateAuthorityBundlePaths)
        {
            dockerComposeServiceYaml.Volumes.Add(new DockerComposeVolumeMappingYaml(Constants.LeapCertificateAuthorityFilePath, path, DockerComposeConstants.Volume.ReadOnly));
        }

        dockerComposeServiceYaml.Environment["NODE_EXTRA_CA_CERTS"] = wellKnownLinuxCertificateAuthorityBundlePaths[0];

        if ("https".Equals(dockerRunner.Protocol, StringComparison.OrdinalIgnoreCase))
        {
            var randomContainerCrtPath = $"/etc/ssl/leap-{Guid.NewGuid()}.crt";
            var randomContainerKeyPath = $"/etc/ssl/leap-{Guid.NewGuid()}.key";

            dockerComposeServiceYaml.Volumes.Add(new DockerComposeVolumeMappingYaml(Constants.LocalCertificateCrtFilePath, randomContainerCrtPath, DockerComposeConstants.Volume.ReadOnly));
            dockerComposeServiceYaml.Volumes.Add(new DockerComposeVolumeMappingYaml(Constants.LocalCertificateKeyFilePath, randomContainerKeyPath, DockerComposeConstants.Volume.ReadOnly));

            dockerComposeServiceYaml.Environment["ASPNETCORE_Kestrel__Certificates__Default__Path"] = randomContainerCrtPath;
            dockerComposeServiceYaml.Environment["ASPNETCORE_Kestrel__Certificates__Default__KeyPath"] = randomContainerKeyPath;
        }

        foreach (var (name, value) in service.GetServiceAndRunnerEnvironmentVariables())
        {
            dockerComposeServiceYaml.Environment[name] = value;
        }

        this._dockerCompose.Configuration.Services[service.ContainerName] = dockerComposeServiceYaml;

        this._aspire.Builder.AddExternalContainer(new ExternalContainerResource(service.Name, service.ContainerName)
        {
            Urls = [service.LocalhostUrl]
        });
    }

    private static IEnumerable<string> GetDockerExtraHostsRuntimeArgs(ApplicationState state)
    {
        yield return "--add-host";
        yield return "host.docker.internal:host-gateway";

        var uniqueCustomHosts = state.Services.Values
            .Select(x => x.Ingress.Host)
            .Where(x => !x.IsLocalhost)
            .Select(x => x.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var host in uniqueCustomHosts)
        {
            yield return "--add-host";
            yield return host + ":host-gateway";
        }
    }

    private void HandleDotnetRunner(Service service, DotnetRunner dotnetRunner)
    {
        var workingDirectoryPath = Path.GetDirectoryName(dotnetRunner.ProjectPath)!;

        // dotnet watch arguments inspired by .NET Aspire:
        // https://github.com/dotnet/aspire/blob/v8.0.1/src/Aspire.Hosting/Dcp/ApplicationExecutor.cs#L1004-L1022
        string[] dotnetRunArgs = dotnetRunner.Watch
            ? ["watch", "--project", dotnetRunner.ProjectPath, "--no-launch-profile", "--non-interactive", "--no-hot-reload"]
            : ["run", "--project", dotnetRunner.ProjectPath, "--no-launch-profile"];

        // TODO shall we sanitize the name of the service? Get inspiration from Dapr
        this._aspire.Builder
            .AddExecutable(service.Name, "dotnet", workingDirectoryPath, dotnetRunArgs)
            .WithEndpoint(scheme: dotnetRunner.Protocol, port: service.Ingress.LocalhostPort, isProxied: false, env: "PORT")
            .WithEnvironment("ASPNETCORE_URLS", dotnetRunner.Protocol + "://*:" + service.Ingress.LocalhostPort)
            .WithEnvironment(context =>
            {
                if ("https".Equals(dotnetRunner.Protocol, StringComparison.OrdinalIgnoreCase))
                {
                    context.EnvironmentVariables["ASPNETCORE_Kestrel__Certificates__Default__Path"] = Constants.LocalCertificateCrtFilePath;
                    context.EnvironmentVariables["ASPNETCORE_Kestrel__Certificates__Default__KeyPath"] = Constants.LocalCertificateKeyFilePath;
                }
            })
            .WithEnvironment(service.GetServiceAndRunnerEnvironmentVariables())
            .WithOtlpExporter();
    }

    private void HandleOpenApiRunner(ApplicationState state, Service service, OpenApiRunner openApiRunner)
    {
        // See:
        // https://github.com/stoplightio/prism/blob/v5.5.4/docs/getting-started/01-installation.md#docker
        // https://github.com/stoplightio/prism/blob/v5.5.4/Dockerfile#L69
        const int prismContainerPort = 4010;

        // TODO shall we sanitize the name of the service?
        var builder = this._aspire.Builder.AddContainer(service.Name, "stoplight/prism", tag: "5")
            .WithEndpoint(scheme: "http", port: service.Ingress.LocalhostPort, targetPort: prismContainerPort)
            .WithContainerRuntimeArgs([.. GetDockerExtraHostsRuntimeArgs(state)]);

        if (openApiRunner.IsUrl)
        {
            builder.WithArgs(["mock", "--host", "0.0.0.0", "--dynamic", openApiRunner.Specification]);
        }
        else
        {
            // We can't use the built-in "WithBindMount()" to mount the spec file because it would attempt to make the source spec path
            // relative to the Aspire app host directory, but our spec path is already consolidated and absolute. See:
            // https://github.com/dotnet/aspire/blob/v8.0.1/src/Aspire.Hosting/ContainerResourceBuilderExtensions.cs#L79
            builder.WithArgs(["mock", "--host", "0.0.0.0", "--dynamic", "/tmp/swagger.yml"])
                .WithAnnotation(new ContainerMountAnnotation(openApiRunner.Specification, "/tmp/swagger.yml", ContainerMountType.BindMount, isReadOnly: true));
        }
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}