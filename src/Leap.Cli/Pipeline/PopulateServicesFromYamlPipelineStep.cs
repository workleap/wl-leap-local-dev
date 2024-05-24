using System.Text.RegularExpressions;
using Leap.Cli.Configuration;
using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Model.Traits;
using Leap.Cli.Platform;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class PopulateServicesFromYamlPipelineStep : IPipelineStep
{
    private static readonly HashSet<string> SupportedBackendProtocols = ["http", "https"];
    private readonly ILeapYamlAccessor _leapYamlAccessor;
    private readonly IPortManager _portManager;
    private readonly ILogger _logger;

    public PopulateServicesFromYamlPipelineStep(
        ILeapYamlAccessor leapYamlAccessor,
        IPortManager portManager,
        ILogger<PopulateServicesFromYamlPipelineStep> logger)
    {
        this._leapYamlAccessor = leapYamlAccessor;
        this._portManager = portManager;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
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
                    if (ingressYaml.Host != null)
                    {
                        service.Ingress.Host = ingressYaml.Host;
                        if (!IsDomainSupported(service.Ingress.Host))
                        {
                            this._logger.LogWarning("A service '{Service}' has an invalid host '{Host}' in the configuration file '{Path}'. The service will be ignored.", serviceName, service.Ingress.Host, leapConfig.Path);
                            continue;
                        }
                    }

                    if (ingressYaml.Port is { } port)
                    {
                        if (!this._portManager.TryRegisterPort(port, out var reason))
                        {
                            this._logger.LogWarning("A service '{Service}' has an invalid port '{Port}' in the configuration file '{Path}'. The port is {Reason}. The service will be ignored.", serviceName, port, leapConfig.Path, reason.Value.ToString());
                            continue;
                        }

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

                if (runnerYaml is IHasPort hasPortYaml)
                {
                    if (hasPortYaml.Port is { } port)
                    {
                        if (!this._portManager.TryRegisterPort(port, out var reason))
                        {
                            this._logger.LogWarning("A service '{Service}' contains a runner expected to launch and bind to an invalid port '{Port}' in the configuration file '{Path}'. The port is {Reason}. The service will be ignored.", serviceName, port, leapConfig.Path, reason.Value);
                            continue;
                        }
                    }
                }

                if (runnerYaml is IHasProtocol hasProtocolYaml)
                {
                    if (string.IsNullOrWhiteSpace(hasProtocolYaml.Protocol))
                    {
                        hasProtocolYaml.Protocol = "http";
                    }
                    else if (!SupportedBackendProtocols.Contains(hasProtocolYaml.Protocol))
                    {
                        this._logger.LogWarning("A runner has an invalid protocol '{Protocol}' in the configuration file '{Path}'. The service '{Service}' will be ignored.", hasProtocolYaml.Protocol, leapConfig.Path, service.Name);
                        continue;
                    }
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

                    service.Runners.Add(new DotnetRunner
                    {
                        ProjectPath = projectPath,
                        Port = dotnetRunnerYaml.Port,
                        Protocol = dotnetRunnerYaml.Protocol,
                    });
                }
                else if (runnerYaml is OpenApiRunnerYaml openApiRunnerYaml)
                {
                    var specPathOrUrl = openApiRunnerYaml.Specification;

                    if (string.IsNullOrEmpty(specPathOrUrl))
                    {
                        this._logger.LogWarning("An OpenAPI mock server runner is missing an OpenAPI specification path or URL in the configuration file '{Path}'. The service '{Service}' will be ignored.", leapConfig.Path, service.Name);
                        continue;
                    }

                    var isUrl = false;
                    if (Uri.TryCreate(specPathOrUrl, UriKind.Absolute, out var specUrl) && SupportedBackendProtocols.Contains(specUrl.Scheme))
                    {
                        var builder = new UriBuilder(specUrl);
                        if (builder.Host.ToLowerInvariant() is "127.0.0.1" or "localhost")
                        {
                            // Prism OpenAPI mock server is running in a container and can only access the host through host.docker.internal
                            builder.Host = "host.docker.internal";
                            specPathOrUrl = builder.Uri.ToString();
                        }

                        isUrl = true;
                    }
                    else
                    {
                        specPathOrUrl = EnsureAbsolutePath(specPathOrUrl, leapConfig);

                        if (!File.Exists(specPathOrUrl))
                        {
                            this._logger.LogWarning("An OpenAPI mock server runner is missing an OpenAPI specification '{Path}' in the configuration file '{Path}'. The service '{Service}' will be ignored.", specPathOrUrl, leapConfig.Path, service.Name);
                            continue;
                        }
                    }

                    service.Runners.Add(new OpenApiRunner
                    {
                        Specification = specPathOrUrl,
                        IsUrl = isUrl,
                        Port = openApiRunnerYaml.Port,
                        Protocol = "http",
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
                    this._logger.LogWarning("A service '{Service}' has an unknown runner type in the configuration file '{Path}'. The service will be ignored.", service.Name, leapConfig.Path);
                    continue;
                }

                if (service.Runners.Count > 1)
                {
                    this._logger.LogWarning("A service '{Service}' has more than one runner in the configuration file '{Path}'. The '{RunnerKind}' runner will be used as it is the first declared.", service.Name, leapConfig.Path, service.Runners[0]);
                }

                service.ActiveRunner = service.Runners[0];

                if (!state.Services.TryAdd(service.Name, service))
                {
                    this._logger.LogWarning("A service '{Service}' is defined multiple times in the configuration file '{Path}'. Only the first definition will be used.", service.Name, leapConfig.Path);
                }
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

    private static bool IsDomainSupported(string domain)
    {
        foreach (var pattern in Constants.SupportedLocalDevelopmentCertificateDomainNames)
        {
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            if (Regex.IsMatch(domain, regexPattern, RegexOptions.IgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
