using System.Text.RegularExpressions;
using Leap.Cli.Configuration;
using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Model.Traits;
using Leap.Cli.Platform;
using Leap.Cli.Yaml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leap.Cli.Pipeline;

internal sealed class PopulateServicesFromYamlPipelineStep : IPipelineStep
{
    private static readonly HashSet<string> SupportedBackendProtocols = new(["http", "https"], StringComparer.OrdinalIgnoreCase);

    // Matches valid paths separated by a single slash with no trailing slashes. Ex: /foo/bar/fizz/buzz
    private static readonly Regex ValidIngressPathPattern = new("^((/[a-zA-Z][a-zA-Z0-9-_]*)+|/)$");

    // Validates that user-defined hosts match one of the supported wildcard domains of our certificate,
    // and only allow 3-parts subdomains (ex: foo.workleap.localhost) as a wildcard (*) does not allow dots.
    internal static readonly Regex SupportedWildcardLocalhostDomainNamesRegex = new(
        string.Join('|', Constants.SupportedWildcardLocalhostDomainNames.Select(ConvertWildcardDomainToPattern)),
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly ILeapYamlAccessor _leapYamlAccessor;
    private readonly IPortManager _portManager;
    private readonly ILogger _logger;
    private readonly IOptions<LeapGlobalOptions> _options;

    public PopulateServicesFromYamlPipelineStep(
        ILeapYamlAccessor leapYamlAccessor,
        IPortManager portManager,
        IOptions<LeapGlobalOptions> options,
        ILogger<PopulateServicesFromYamlPipelineStep> logger)
    {
        this._leapYamlAccessor = leapYamlAccessor;
        this._portManager = portManager;
        this._options = options;
        this._logger = logger;
    }

    private static string ConvertWildcardDomainToPattern(string domain) => $"^{Regex.Escape(domain).Replace("\\*", "[a-z0-9]+(-[a-z0-9]+)*")}$";

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var leapYamls = await this._leapYamlAccessor.GetAllAsync(cancellationToken);

        foreach (var leapYaml in leapYamls)
        {
            if (leapYaml.Content.Services == null)
            {
                continue;
            }

            foreach (var (serviceName, serviceYaml) in leapYaml.Content.Services)
            {
                if (string.IsNullOrWhiteSpace(serviceName))
                {
                    this._logger.LogWarning("A service is missing a name in the configuration file '{Path}' and will be ignored.", leapYaml.Path);
                    continue;
                }

                if (serviceYaml == null)
                {
                    this._logger.LogWarning("A service '{Service}' does not have a definition in the configuration file '{Path}' and will be ignored.", serviceName, leapYaml.Path);
                    continue;
                }

                var service = new ServiceYamlConverter(this._logger, this._portManager, leapYaml, serviceYaml).Convert(serviceName);

                if (service != null && this.ShouldStartService(service))
                {
                    if (!state.Services.TryAdd(service.Name, service))
                    {
                        this._logger.LogWarning("A service with the name '{Service}' is defined multiple times in the configuration file '{Path}'. Only the first definition will be used.", service.Name, leapYaml.Path);
                    }
                }
            }
        }
    }

    private bool ShouldStartService(Service service)
    {
        if (service.Profiles.Count == 0 || this._options.Value.Profiles.Length == 0)
        {
            return true;
        }

        return service.Profiles.Overlaps(this._options.Value.Profiles);
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private sealed class ServiceYamlConverter(ILogger logger, IPortManager portManager, LeapYamlFile leapYaml, ServiceYaml serviceYaml)
    {
        public Service? Convert(string serviceName)
        {
            try
            {
                // TODO validate the service name (format/convention TBD)
                var service = new Service(serviceName, leapYaml);

                this.ConvertIngress(service);
                this.ConvertEnvironmentVariables(service);
                this.ConvertRunners(service);
                this.ConvertProfiles(service);

                return service;
            }
            catch (LeapYamlConversionException ex)
            {
                logger.LogWarning("{ConversionExceptionMessage}", ex.Message);
            }

            return null;
        }

        private void ConvertProfiles(Service service)
        {
            if (serviceYaml.Profiles == null)
            {
                return;
            }

            foreach (var profile in serviceYaml.Profiles)
            {
                if (!string.IsNullOrWhiteSpace(profile))
                {
                    service.Profiles.Add(profile);
                }
            }
        }

        private void ConvertIngress(Service service)
        {
            if (serviceYaml.Ingress is { } ingressYaml)
            {
                this.ConvertIngressHost(service, ingressYaml);
                this.ConvertIngressPath(service, ingressYaml);
            }
        }

        private void ConvertIngressHost(Service service, IngressYaml ingressYaml)
        {
            if (ingressYaml.Host == null)
            {
                return;
            }

            if (!SupportedWildcardLocalhostDomainNamesRegex.IsMatch(ingressYaml.Host))
            {
                throw new LeapYamlConversionException($"A service '{service.Name}' has an invalid host '{ingressYaml.Host}' in the configuration file '{leapYaml.Path}'. Host must match one of the supported wildcard domains '{string.Join(", ", Constants.SupportedWildcardLocalhostDomainNames)}', where '*' allows alphanumeric and hyphen characters. The service will be ignored.");
            }

            service.Ingress.Host = ingressYaml.Host;
        }

        private void ConvertIngressPath(Service service, IngressYaml ingressYaml)
        {
            if (ingressYaml.Path == null)
            {
                return;
            }

            var match = ValidIngressPathPattern.Match(ingressYaml.Path);
            if (match.Success)
            {
                service.Ingress.Path = ingressYaml.Path;
            }
            else
            {
                throw new LeapYamlConversionException($"A service '{service.Name}' has a malformed ingress path: '{ingressYaml.Path}' in the configuration file '{leapYaml.Path}'. The service will be ignored.");
            }
        }

        private void ConvertEnvironmentVariables(Service service)
        {
            CopyEnvironmentVariables(serviceYaml.EnvironmentVariables, service.EnvironmentVariables);
        }

        private static void ConvertEnvironmentVariables(Runner runner, RunnerYaml runnerYaml)
        {
            CopyEnvironmentVariables(runnerYaml.EnvironmentVariables, runner.EnvironmentVariables);
        }

        private static void CopyEnvironmentVariables(KeyValueCollectionYaml? source, Dictionary<string, string> destination)
        {
            if (source != null)
            {
                foreach (var (key, value) in source)
                {
                    destination[key] = value;
                }
            }
        }

        private void ConvertRunners(Service service)
        {
            // TODO for now we only support one runner per service
            var runnerYaml = serviceYaml.Runners?.FirstOrDefault()
                             ?? throw new LeapYamlConversionException($"A service '{service.Name}' is missing a runner in the configuration file '{leapYaml.Path}' and will be ignored.");

            var runner = runnerYaml switch
            {
                ExecutableRunnerYaml exeRunnerYaml => this.ConvertExecutableRunner(service, exeRunnerYaml),
                DockerRunnerYaml dockerRunnerYaml => this.ConvertDockerRunner(service, dockerRunnerYaml),
                DotnetRunnerYaml dotnetRunnerYaml => this.ConvertDotnetRunner(service, dotnetRunnerYaml),
                OpenApiRunnerYaml openApiRunnerYaml => this.ConvertOpenApiRunner(service, openApiRunnerYaml),
                RemoteRunnerYaml remoteRunnerYaml => this.ConvertRemoteRunner(service, remoteRunnerYaml),
                _ => throw new LeapYamlConversionException($"A service '{service.Name}' has an unknown runner type in the configuration file '{leapYaml.Path}'. The service will be ignored.")
            };

            this.ConvertRunnerProtocolAndPort(service, runner, runnerYaml);
            ConvertEnvironmentVariables(runner, runnerYaml);

            service.Runners.Add(runner);

            if (service.Runners.Count > 1)
            {
                logger.LogWarning("A service '{Service}' has more than one runner in the configuration file '{Path}'. The '{RunnerKind}' runner will be used as it is the first declared.", service.Name, leapYaml.Path, service.Runners[0]);
            }

            service.ActiveRunner = service.Runners[0];
        }

        private void ConvertRunnerProtocolAndPort(Service service, Runner runner, RunnerYaml runnerYaml)
        {
            if (runnerYaml is IHasProtocol { Protocol: { } protocol })
            {
                runner.Protocol = SupportedBackendProtocols.Contains(protocol)
                    ? protocol.ToLowerInvariant()
                    : throw new LeapYamlConversionException($"A runner has an invalid protocol '{protocol}' in the configuration file '{leapYaml.Path}'. The service '{service.Name}' will be ignored.");
            }

            if (runnerYaml is IHasPort { Port: { } port })
            {
                runner.Port = portManager.TryRegisterPort(port, out var reason)
                    ? port
                    : throw new LeapYamlConversionException($"A service '{service.Name}' contains a runner expected to launch and bind to an invalid port '{port}' in the configuration file '{leapYaml.Path}'. The port is {reason.Value}. The service will be ignored.");
            }
        }

        private Runner ConvertExecutableRunner(Service service, ExecutableRunnerYaml exeRunnerYaml)
        {
            if (string.IsNullOrWhiteSpace(exeRunnerYaml.Command))
            {
                throw new LeapYamlConversionException($"An executable runner is missing a command in the configuration file '{leapYaml.Path}'. The service '{service.Name}' will be ignored.");
            }

            var arguments = exeRunnerYaml.Arguments?.Where(x => !string.IsNullOrEmpty(x)).Cast<string>().ToArray() ?? [];

            if (string.IsNullOrWhiteSpace(exeRunnerYaml.WorkingDirectory))
            {
                throw new LeapYamlConversionException($"An executable runner is missing a working directory in the configuration file '{leapYaml.Path}'. The service '{service.Name}' will be ignored.");
            }

            var workingDirectory = EnsureAbsolutePath(exeRunnerYaml.WorkingDirectory, leapYaml);

            return new ExecutableRunner
            {
                Command = exeRunnerYaml.Command,
                Arguments = [.. arguments],
                WorkingDirectory = workingDirectory,
            };
        }

        private Runner ConvertDockerRunner(Service service, DockerRunnerYaml dockerRunnerYaml)
        {
            if (string.IsNullOrWhiteSpace(dockerRunnerYaml.ImageAndTag))
            {
                throw new LeapYamlConversionException($"A Docker image is missing a path in the configuration file '{leapYaml.Path}'. The service '{service.Name}' will be ignored.");
            }

            var containerPort = dockerRunnerYaml.ContainerPort;
            if (!containerPort.HasValue)
            {
                throw new LeapYamlConversionException($"A Docker image is missing a container port in the configuration file '{leapYaml.Path}'. The service '{service.Name}' will be ignored.");
            }

            if (!portManager.IsPortInValidRange(containerPort.Value))
            {
                throw new LeapYamlConversionException($"A Docker image has an invalid container port '{containerPort.Value}' in the configuration file '{leapYaml.Path}'. The service '{service.Name}' will be ignored.");
            }

            var dockerVolumeMappings = new List<DockerRunnerVolumeMapping>();

            foreach (var volumeYaml in dockerRunnerYaml.Volumes ?? [])
            {
                if (volumeYaml == null || string.IsNullOrWhiteSpace(volumeYaml.Source) || string.IsNullOrWhiteSpace(volumeYaml.Destination))
                {
                    throw new LeapYamlConversionException($"A Docker image has an invalid volume mapping in the configuration file '{leapYaml.Path}'. The service '{service.Name}' will be ignored.");
                }

                var sourcePath = EnsureAbsolutePath(volumeYaml.Source, leapYaml);
                if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                {
                    throw new LeapYamlConversionException($"A Docker image has an invalid volume mapping source '{volumeYaml.Source}' in the configuration file '{leapYaml.Path}'. The mapping source must be an existing file or directory. The service '{service.Name}' will be ignored.");
                }

                var destinationPath = volumeYaml.Destination;
                if (!Path.IsPathRooted(destinationPath))
                {
                    throw new LeapYamlConversionException($"A Docker image has an invalid volume mapping destination '{volumeYaml.Destination}' in the configuration file '{leapYaml.Path}'. It must be an absolute path. The service '{service.Name}' will be ignored.");
                }

                dockerVolumeMappings.Add(new DockerRunnerVolumeMapping(sourcePath, destinationPath));
            }

            return new DockerRunner
            {
                ImageAndTag = dockerRunnerYaml.ImageAndTag,
                ContainerPort = containerPort.Value,
                Volumes = [.. dockerVolumeMappings],
            };
        }

        private Runner ConvertDotnetRunner(Service service, DotnetRunnerYaml dotnetRunnerYaml)
        {
            if (string.IsNullOrWhiteSpace(dotnetRunnerYaml.ProjectPath))
            {
                throw new LeapYamlConversionException($"A .NET project runner is missing a project path in the configuration file '{leapYaml.Path}'. The service '{service.Name}' will be ignored.");
            }

            var projectPath = EnsureAbsolutePath(dotnetRunnerYaml.ProjectPath, leapYaml);

            if (!File.Exists(projectPath))
            {
                throw new LeapYamlConversionException($"A .NET project runner references a project that does not exist at '{projectPath}' in the configuration file '{leapYaml.Path}'. The service '{service.Name}' will be ignored.");
            }

            return new DotnetRunner
            {
                ProjectPath = projectPath,
                Watch = dotnetRunnerYaml.Watch.GetValueOrDefault(true),
            };
        }

        private Runner ConvertOpenApiRunner(Service service, OpenApiRunnerYaml openApiRunnerYaml)
        {
            var specPathOrUrl = openApiRunnerYaml.Specification;

            if (string.IsNullOrEmpty(specPathOrUrl))
            {
                throw new LeapYamlConversionException($"An OpenAPI mock server runner is missing an OpenAPI specification path or URL in the configuration file '{leapYaml.Path}'. The service '{service.Name}' will be ignored.");
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
                specPathOrUrl = EnsureAbsolutePath(specPathOrUrl, leapYaml);

                if (!File.Exists(specPathOrUrl))
                {
                    throw new LeapYamlConversionException($"An OpenAPI mock server runner is missing an OpenAPI specification '{specPathOrUrl}' in the configuration file '{leapYaml.Path}'. The service '{service.Name}' will be ignored.");
                }
            }

            return new OpenApiRunner
            {
                Specification = specPathOrUrl,
                IsUrl = isUrl,
            };
        }

        private Runner ConvertRemoteRunner(Service service, RemoteRunnerYaml remoteRunnerYaml)
        {
            if (!Uri.TryCreate(remoteRunnerYaml.Url, UriKind.Absolute, out var url))
            {
                throw new LeapYamlConversionException($"A remote runner has an invalid URL '{remoteRunnerYaml.Url}' in the configuration file '{leapYaml.Path}'. The service '{service.Name}' will be ignored.");
            }

            return new RemoteRunner
            {
                Url = url.OriginalString,
            };
        }

        private static string EnsureAbsolutePath(string path, LeapYamlFile leapYaml)
        {
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Path.GetDirectoryName(leapYaml.Path)!, path);
            }

            // Fix slashes
            // Based on: https://github.com/dotnet/aspire/blob/v8.0.0-preview.2.23619.3/src/Aspire.Hosting/Utils/PathNormalizer.cs
            path = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            path = Path.GetFullPath(path);

            return path;
        }
    }

    private sealed class LeapYamlConversionException(string message) : Exception(message);
}