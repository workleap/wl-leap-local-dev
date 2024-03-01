using Leap.Cli.Configuration;
using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Extensions;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class PopulateServicesFromYamlPipelineStep : IPipelineStep
{
    private static readonly HashSet<string> SupportedBackendProtocols = ["http", "https"];
    private readonly IFeatureManager _featureManager;
    private readonly ILeapYamlAccessor _leapYamlAccessor;
    private readonly IPortManager _portManager;
    private readonly ILogger _logger;

    public PopulateServicesFromYamlPipelineStep(
        IFeatureManager featureManager,
        ILeapYamlAccessor leapYamlAccessor,
        IPortManager portManager,
        ILogger<PopulateServicesFromYamlPipelineStep> logger)
    {
        this._featureManager = featureManager;
        this._leapYamlAccessor = leapYamlAccessor;
        this._portManager = portManager;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (!this._featureManager.IsEnabled(FeatureIdentifiers.LeapPhase2))
        {
            this._logger.LogPipelineStepSkipped(nameof(PopulateServicesFromYamlPipelineStep), FeatureIdentifiers.LeapPhase2);
            return;
        }

        var leapConfigs = await this._leapYamlAccessor.GetAllAsync(cancellationToken);

        foreach (var leapConfig in leapConfigs)
        {
            if (leapConfig.Content.Services == null)
            {
                continue;
            }

            // TODO this is an extremely ugly loop, do some refactoring
            foreach (var (serviceName, serviceYaml) in leapConfig.Content.Services)
            {
                if (string.IsNullOrWhiteSpace(serviceName))
                {
                    this._logger.LogWarning("A service is missing a name in the configuration file '{Path}' and will be ignored.", leapConfig.Path);
                    continue;
                }

                if (serviceYaml == null)
                {
                    this._logger.LogWarning("A service '{Service}' does not have a definition in the configuration file '{Path}' and will be ignored.", serviceName, leapConfig.Path);
                    continue;
                }

                // TODO validate the service name (format/convention TBD)
                var service = new Service
                {
                    Name = serviceName,
                };

                if (serviceYaml.Ingress is { } ingressYaml)
                {
                    // TODO validate host against the supported hosts from the local certificate
                    // so like "demo.workleap.local" is OK but something like "demo.mydomain.com" is not
                    if (ingressYaml.Host != null)
                    {
                        service.Ingress.Host = ingressYaml.Host;
                    }

                    // TODO validate is a valid URL port, not already occupied by other services
                    if (ingressYaml.Port is { } port)
                    {
                        service.Ingress.ExternalPort = port;
                    }

                    // TODO validate is a valid URL path part
                    if (ingressYaml.Path != null)
                    {
                        service.Ingress.Path = ingressYaml.Path;
                    }
                }

                // TODO for now we only support one runner per service
                var runnerYaml = serviceYaml.Runners?.FirstOrDefault();
                if (runnerYaml == null)
                {
                    this._logger.LogWarning("A service '{Service}' is missing a runner in the configuration file '{Path}' and will be ignored.", service.Name, leapConfig.Path);
                    continue;
                }

                if (runnerYaml is ExecutableRunnerYaml exeRunnerYaml)
                {
                    if (string.IsNullOrEmpty(exeRunnerYaml.Command))
                    {
                        this._logger.LogWarning("An executable runner is missing a command in the configuration file '{Path}'. The service '{Service}' will be ignored.", leapConfig.Path, service.Name);
                        continue;
                    }

                    var arguments = new List<string>();

                    if (exeRunnerYaml.Arguments != null)
                    {
                        foreach (var argument in exeRunnerYaml.Arguments)
                        {
                            if (string.IsNullOrEmpty(argument))
                            {
                                continue;
                            }

                            arguments.Add(argument);
                        }
                    }

                    var workingDirectory = exeRunnerYaml.WorkingDirectory;
                    if (workingDirectory == null)
                    {
                        this._logger.LogWarning("An executable runner is missing a working directory in the configuration file '{Path}'. The service '{Service}' will be ignored.", leapConfig.Path, service.Name);
                        continue;
                    }

                    workingDirectory = EnsureAbsolutePath(workingDirectory, leapConfig);

                    if (exeRunnerYaml.Port.HasValue && !this._portManager.IsPortInValidRange(exeRunnerYaml.Port.Value))
                    {
                        this._logger.LogWarning("An executable runner has an invalid port '{Port}' in the configuration file '{Path}'. The service '{Service}' will be ignored.", exeRunnerYaml.Port.Value, leapConfig.Path, service.Name);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(exeRunnerYaml.Protocol))
                    {
                        exeRunnerYaml.Protocol = "http";
                    }
                    else if (!SupportedBackendProtocols.Contains(exeRunnerYaml.Protocol))
                    {
                        this._logger.LogWarning("An executable runner has an invalid protocol '{Protocol}' in the configuration file '{Path}'. The service '{Service}' will be ignored.", exeRunnerYaml.Protocol, leapConfig.Path, service.Name);
                        continue;
                    }

                    service.Runners.Add(new ExecutableRunner
                    {
                        Command = exeRunnerYaml.Command,
                        Arguments = arguments.ToArray(),
                        WorkingDirectory = workingDirectory,
                        Port = exeRunnerYaml.Port,
                        Protocol = exeRunnerYaml.Protocol,
                    });
                }
                else if (runnerYaml is DockerRunnerYaml dockerRunnerYaml)
                {
                    var dockerImage = dockerRunnerYaml.Image;

                    if (string.IsNullOrWhiteSpace(dockerImage))
                    {
                        this._logger.LogWarning("A Docker image is missing a path in the configuration file '{Path}'. The service '{Service}' will be ignored.", leapConfig.Path, service.Name);
                        continue;
                    }

                    var containerPort = dockerRunnerYaml.ContainerPort;
                    if (!containerPort.HasValue)
                    {
                        this._logger.LogWarning("A Docker image is missing a container port in the configuration file '{Path}'. The service '{Service}' will be ignored.", leapConfig.Path, service.Name);
                        continue;
                    }

                    if (!this._portManager.IsPortInValidRange(containerPort.Value))
                    {
                        this._logger.LogWarning("A Docker image has an invalid container port '{Port}' in the configuration file '{Path}'. The service '{Service}' will be ignored.", containerPort.Value, leapConfig.Path, service.Name);
                        continue;
                    }

                    if (dockerRunnerYaml.HostPort.HasValue && !this._portManager.TryRegisterPort(dockerRunnerYaml.HostPort.Value, out var reason))
                    {
                        this._logger.LogWarning("A Docker image has an invalid host port '{Port}' in the configuration file '{Path}'. Reason: '{Reason'}. The service '{Service}' will be ignored.", containerPort.Value, leapConfig.Path, reason, service.Name);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(dockerRunnerYaml.Protocol))
                    {
                        dockerRunnerYaml.Protocol = "http";
                    }
                    else if (!SupportedBackendProtocols.Contains(dockerRunnerYaml.Protocol))
                    {
                        this._logger.LogWarning("A Docker image has an invalid protocol '{Protocol}' in the configuration file '{Path}'. The service '{Service}' will be ignored.", dockerRunnerYaml.Protocol, leapConfig.Path, service.Name);
                        continue;
                    }

                    service.Runners.Add(new DockerRunner
                    {
                        Image = dockerImage,
                        ContainerPort = containerPort.Value,
                        HostPort = dockerRunnerYaml.HostPort,
                        Protocol = dockerRunnerYaml.Protocol,
                    });
                }
                else if (runnerYaml is DotnetRunnerYaml dotnetRunnerYaml)
                {
                    var projectPath = dotnetRunnerYaml.ProjectPath;

                    if (string.IsNullOrWhiteSpace(projectPath))
                    {
                        this._logger.LogWarning("A .NET project runner is missing a project path in the configuration file '{Path}'. The service '{Service}' will be ignored.", leapConfig.Path, service.Name);
                        continue;
                    }

                    projectPath = EnsureAbsolutePath(projectPath, leapConfig);

                    if (dotnetRunnerYaml.Port.HasValue && !this._portManager.TryRegisterPort(dotnetRunnerYaml.Port.Value, out var reason))
                    {
                        this._logger.LogWarning("A .NET project runner has an invalid port '{Port}' in the configuration file '{Path}'. Reason: '{Reason}'. The service '{Service}' will be ignored.", dotnetRunnerYaml.Port, leapConfig.Path, reason, service.Name);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(dotnetRunnerYaml.Protocol))
                    {
                        dotnetRunnerYaml.Protocol = "http";
                    }
                    else if (!SupportedBackendProtocols.Contains(dotnetRunnerYaml.Protocol))
                    {
                        this._logger.LogWarning("A .NET project runner has an invalid protocol '{Protocol}' in the configuration file '{Path}'. The service '{Service}' will be ignored.", dotnetRunnerYaml.Protocol, leapConfig.Path, service.Name);
                        continue;
                    }

                    service.Runners.Add(new DotnetRunner
                    {
                        ProjectPath = projectPath,
                        Port = dotnetRunnerYaml.Port,
                        Protocol = dotnetRunnerYaml.Protocol,
                    });
                }
                else if (runnerYaml is OpenApiRunnerYaml openApiRunnerYaml)
                {
                    // TODO validate that the files actually exists
                    // TODO also support URLS?
                    var specPath = openApiRunnerYaml.Specification;

                    if (string.IsNullOrEmpty(specPath))
                    {
                        this._logger.LogWarning("An OpenAPI mock server runner is missing a specification path in the configuration file '{Path}'. The service '{Service}' will be ignored.", leapConfig.Path, service.Name);
                        continue;
                    }

                    specPath = EnsureAbsolutePath(specPath, leapConfig);

                    if (openApiRunnerYaml.Port.HasValue && !this._portManager.TryRegisterPort(openApiRunnerYaml.Port.Value, out var reason))
                    {
                        this._logger.LogWarning("An OpenAPI mock server runner has an invalid port '{Port}' in the configuration file '{Path}'. Reason: '{Reason}'. The service '{Service}' will be ignored.", openApiRunnerYaml.Port, leapConfig.Path, reason, service.Name);
                        continue;
                    }

                    service.Runners.Add(new OpenApiRunner
                    {
                        Specification = specPath,
                        Port = openApiRunnerYaml.Port,
                    });
                }
                else if (runnerYaml is RemoteRunnerYaml remoteRunnerYaml)
                {
                    if (!Uri.TryCreate(remoteRunnerYaml.Url, UriKind.Absolute, out var url))
                    {
                        this._logger.LogWarning("A remote runner has an invalid URL '{Url}' in the configuration file '{Path}'. The service '{Service}' will be ignored.", remoteRunnerYaml.Url, leapConfig.Path, service.Name);
                        continue;
                    }

                    service.Runners.Add(new RemoteRunner
                    {
                        Url = url.OriginalString,
                        Port = url.Port,
                    });
                }
                else
                {
                    // TODO warn that a service has an unknown runner type (print path to yaml file)
                    continue;
                }

                // TODO again, for now we only support one runner per service
                service.ActiveRunner = service.Runners[0];

                // TODO prevent multiple services with the same name
                state.Services[service.Name] = service;
            }
        }
    }

    private static string EnsureAbsolutePath(string path, LeapYamlFile config)
    {
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(Path.GetDirectoryName(config.Path)!, path);
        }

        // Fix slashes
        // Based on: https://github.com/dotnet/aspire/blob/v8.0.0-preview.2.23619.3/src/Aspire.Hosting/Utils/PathNormalizer.cs
        path = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        path = Path.GetFullPath(path);

        return path;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
