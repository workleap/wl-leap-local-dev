using Leap.Cli.Configuration;
using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Model;
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

                // TODO for now we only support one binding per service
                var bindingYaml = serviceYaml.Bindings?.FirstOrDefault();
                if (bindingYaml == null)
                {
                    this._logger.LogWarning("A service '{Service}' is missing a binding in the configuration file '{Path}' and will be ignored.", service.Name, leapConfig.Path);
                    continue;
                }

                if (bindingYaml is ExecutableBindingYaml exeBindingYaml)
                {
                    if (string.IsNullOrEmpty(exeBindingYaml.Command))
                    {
                        this._logger.LogWarning("An executable binding is missing a command in the configuration file '{Path}'. The service '{Service}' will be ignored.", leapConfig.Path, service.Name);
                        continue;
                    }

                    var arguments = new List<string>();

                    if (exeBindingYaml.Arguments != null)
                    {
                        foreach (var argument in exeBindingYaml.Arguments)
                        {
                            if (string.IsNullOrEmpty(argument))
                            {
                                continue;
                            }

                            arguments.Add(argument);
                        }
                    }

                    var workingDirectory = exeBindingYaml.WorkingDirectory;
                    if (workingDirectory != null)
                    {
                        workingDirectory = EnsureAbsolutePath(workingDirectory, leapConfig);
                    }

                    if (exeBindingYaml.Port.HasValue && !this._portManager.IsPortInValidRange(exeBindingYaml.Port.Value))
                    {
                        this._logger.LogWarning("An executable binding has an invalid port '{Port}' in the configuration file '{Path}'. The service '{Service}' will be ignored.", exeBindingYaml.Port.Value, leapConfig.Path, service.Name);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(exeBindingYaml.Protocol))
                    {
                        exeBindingYaml.Protocol = "http";
                    }
                    else if (!SupportedBackendProtocols.Contains(exeBindingYaml.Protocol, StringComparer.OrdinalIgnoreCase))
                    {
                        this._logger.LogWarning("An executable binding has an invalid protocol '{Protocol}' in the configuration file '{Path}'. The service '{Service}' will be ignored.", exeBindingYaml.Protocol, leapConfig.Path, service.Name);
                        continue;
                    }

                    service.Bindings.Add(new ExecutableBinding
                    {
                        Command = exeBindingYaml.Command,
                        Arguments = arguments.ToArray(),
                        WorkingDirectory = workingDirectory,
                        Port = exeBindingYaml.Port,
                        Protocol = exeBindingYaml.Protocol,
                    });
                }
                else if (bindingYaml is DockerBindingYaml dockerBindingYaml)
                {
                    var dockerImage = dockerBindingYaml.Image;

                    if (string.IsNullOrWhiteSpace(dockerImage))
                    {
                        this._logger.LogWarning("A Docker image is missing a path in the configuration file '{Path}'. The service '{Service}' will be ignored.", leapConfig.Path, service.Name);
                        continue;
                    }

                    var containerPort = dockerBindingYaml.ContainerPort;
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

                    if (dockerBindingYaml.HostPort.HasValue && !this._portManager.TryRegisterPort(dockerBindingYaml.HostPort.Value, out var reason))
                    {
                        this._logger.LogWarning("A Docker image has an invalid host port '{Port}' in the configuration file '{Path}'. Reason: '{Reason'}. The service '{Service}' will be ignored.", containerPort.Value, leapConfig.Path, reason, service.Name);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(dockerBindingYaml.Protocol))
                    {
                        dockerBindingYaml.Protocol = "http";
                    }
                    else if (!SupportedBackendProtocols.Contains(dockerBindingYaml.Protocol, StringComparer.OrdinalIgnoreCase))
                    {
                        this._logger.LogWarning("A Docker image has an invalid protocol '{Protocol}' in the configuration file '{Path}'. The service '{Service}' will be ignored.", dockerBindingYaml.Protocol, leapConfig.Path, service.Name);
                        continue;
                    }

                    service.Bindings.Add(new DockerBinding
                    {
                        Image = dockerImage,
                        ContainerPort = containerPort.Value,
                        HostPort = dockerBindingYaml.HostPort,
                        Protocol = dockerBindingYaml.Protocol,
                    });
                }
                else if (bindingYaml is CsprojBindingYaml csprojBindingYaml)
                {
                    var csprojPath = csprojBindingYaml.Path;

                    if (string.IsNullOrWhiteSpace(csprojPath))
                    {
                        this._logger.LogWarning("A csproj binding is missing a path in the configuration file '{Path}'. The service '{Service}' will be ignored.", leapConfig.Path, service.Name);
                        continue;
                    }

                    csprojPath = EnsureAbsolutePath(csprojPath, leapConfig);

                    if (csprojBindingYaml.Port.HasValue && !this._portManager.TryRegisterPort(csprojBindingYaml.Port.Value, out var reason))
                    {
                        this._logger.LogWarning("A .NET project binding has an invalid port '{Port}' in the configuration file '{Path}'. Reason: '{Reason}'. The service '{Service}' will be ignored.", csprojBindingYaml.Port, leapConfig.Path, reason, service.Name);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(csprojBindingYaml.Protocol))
                    {
                        csprojBindingYaml.Protocol = "http";
                    }
                    else if (!SupportedBackendProtocols.Contains(csprojBindingYaml.Protocol, StringComparer.OrdinalIgnoreCase))
                    {
                        this._logger.LogWarning("A .NET project binding has an invalid protocol '{Protocol}' in the configuration file '{Path}'. The service '{Service}' will be ignored.", csprojBindingYaml.Protocol, leapConfig.Path, service.Name);
                        continue;
                    }

                    service.Bindings.Add(new CsprojBinding
                    {
                        Path = csprojPath,
                        Port = csprojBindingYaml.Port,
                        Protocol = csprojBindingYaml.Protocol,
                    });
                }
                else if (bindingYaml is OpenApiBindingYaml openApiBindingYaml)
                {
                    // TODO validate that the files actually exists
                    // TODO also support URLS?
                    var specPath = openApiBindingYaml.Specification;

                    if (string.IsNullOrEmpty(specPath))
                    {
                        this._logger.LogWarning("An OpenAPI mock server binding is missing a specification path in the configuration file '{Path}'. The service '{Service}' will be ignored.", leapConfig.Path, service.Name);
                        continue;
                    }

                    specPath = EnsureAbsolutePath(specPath, leapConfig);

                    if (openApiBindingYaml.Port.HasValue && !this._portManager.TryRegisterPort(openApiBindingYaml.Port.Value, out var reason))
                    {
                        this._logger.LogWarning("An OpenAPI mock server binding has an invalid port '{Port}' in the configuration file '{Path}'. Reason: '{Reason}'. The service '{Service}' will be ignored.", openApiBindingYaml.Port, leapConfig.Path, reason, service.Name);
                        continue;
                    }

                    service.Bindings.Add(new OpenApiBinding
                    {
                        Specification = specPath,
                        Port = openApiBindingYaml.Port,
                    });
                }
                else if (bindingYaml is RemoteBindingYaml remoteBindingYaml)
                {
                    if (!Uri.TryCreate(remoteBindingYaml.Url, UriKind.Absolute, out var url))
                    {
                        this._logger.LogWarning("A remote binding has an invalid URL '{Url}' in the configuration file '{Path}'. The service '{Service}' will be ignored.", remoteBindingYaml.Url, leapConfig.Path, service.Name);
                        continue;
                    }

                    service.Bindings.Add(new RemoteBinding
                    {
                        Url = url.OriginalString,
                        Port = url.Port,
                    });
                }
                else
                {
                    // TODO warn that a service has an unknown binding type (print path to yaml file)
                    continue;
                }

                // TODO again, for now we only support one binding per service
                service.ActiveBinding = service.Bindings[0];

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