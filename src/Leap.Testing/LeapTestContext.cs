using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Aspire.ResourceService.Proto.V1;
using CliWrap;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Meziantou.Framework;
using Microsoft.Extensions.Logging;
using Polly;

namespace Workleap.Leap.Testing;

public sealed class LeapTestContext : IAsyncDisposable
{
    // Authenticate to the DCP. The password is hardcoded in Leap (https://github.com/workleap/wl-leap-local-dev/blob/a01e615088ff37af99def9b759e162190a158892/src/Leap.Cli/Aspire/AspireManager.cs#L29).
    private static readonly Metadata DcpHeaders = new Metadata
    {
        { "x-resource-service-api-key", "leap" }
    };

    private readonly CancellationTokenSource _cts;
    private readonly CancellationToken _cancellationToken;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, string> _serviceUrls = new(StringComparer.Ordinal);
    private readonly GrpcChannel _dcpChannel;
    private readonly DashboardService.DashboardServiceClient _dashboardServiceClient;

    private readonly List<FullPath> _configurationFiles = [];
    private readonly List<Func<Uri>> _healthCheckUris = [];

    public string? RemoteEnvironmentName { get; }
    public TimeSpan StartLeapTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public bool KillExistingLeapInstancesOnStart { get; set; }
    public bool KillLeapInstanceOnStop { get; set; }
    public string LeapExecutablePath { get; set; } = "leap";
    public IEnumerable<string?>? ExtraLeapRunArguments { get; set; }

    public LeapTestContext(ILoggerFactory loggerFactory, CancellationToken cancellationToken = default)
        : this(loggerFactory, remoteEnvironmentName: null, cancellationToken)
    {
    }

    public LeapTestContext(ILoggerFactory loggerFactory, string? remoteEnvironmentName, CancellationToken cancellationToken = default)
    {
        this._cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        this._cancellationToken = this._cts.Token;
        this._loggerFactory = loggerFactory;
        this._logger = loggerFactory.CreateLogger("Leap.Testing");
        this.RemoteEnvironmentName = string.IsNullOrEmpty(remoteEnvironmentName) ? null : remoteEnvironmentName;
        this._httpClient = HttpClientFactory.Create(null, loggerFactory.CreateLogger("Leap.Testing.Http"));
        this._dcpChannel = this.CreateDcpChannel();
        this._dashboardServiceClient = new DashboardService.DashboardServiceClient(this._dcpChannel);
    }

    public async ValueTask DisposeAsync()
    {
        await this._cts.CancelAsync();
        this._httpClient.Dispose();
        this._dcpChannel.Dispose();
        this._cts.Dispose();
        if (this.KillLeapInstanceOnStop)
        {
            await this.KillLeap();
        }
    }

    public Uri GetUrl(string leapServiceName)
    {
        if (this._serviceUrls.TryGetValue(leapServiceName, out var result))
        {
            return new Uri(result);
        }

        throw new InvalidOperationException($"Service '{leapServiceName}' is not available");
    }

    public Uri GetUrl(string leapServiceName, string relativePath)
    {
        return new Uri(this.GetUrl(leapServiceName), relativePath);
    }

    public async Task Start()
    {
        if (this.KillExistingLeapInstancesOnStart)
        {
            await this.KillLeap();
        }

        var existingInstances = Process.GetProcessesByName("leap");
        if (existingInstances.Length > 0)
        {
            if (this.KillExistingLeapInstancesOnStart)
            {
                throw new InvalidOperationException("leap is still running after killing it");
            }
            else
            {
                try
                {
                    await this.InitializeDcpResourceUrls();
                    if (await this.IsHealthy())
                    {
                        await OnLeapStarted();
                        return;
                    }
                }
                catch
                {
                    // ignore errors, continue with the slower path
                }
            }
        }

        var stopwatch = Stopwatch.StartNew();
        bool HasTimedOut() => stopwatch.Elapsed > this.StartLeapTimeout;

        string[] remoteEnvArgs = !string.IsNullOrEmpty(this.RemoteEnvironmentName) ? ["--remote-env", this.RemoteEnvironmentName] : [];
        var extraArgs = this.ExtraLeapRunArguments?.Where(value => value is not null) ?? [];
        var isReady = new TaskCompletionSource();
        this._logger.LogInformation("Starting leap");
        var command = Cli.Wrap(this.LeapExecutablePath)
            .WithArguments(["--skip-version-check", "run", .. remoteEnvArgs, "--file", .. this._configurationFiles, .. extraArgs!])
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
            {
                this._logger.LogInformation("leap: {StdOut}", line);
                if (line.Contains("Press Ctrl+C to stop Leap", StringComparison.Ordinal))
                {
                    isReady.SetResult();
                }
            }))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => this._logger.LogError("leap: {StdErr}", line)))
            .ExecuteAsync(CancellationToken.None);

        await ((Task)Task.WhenAny(command.Task, isReady.Task).WaitAsync(this.StartLeapTimeout, this._cancellationToken)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        // Wait for leap and all resources to be ready
        while (!await this.IsHealthy())
        {
            // The leap process has ended
            if (command.Task.IsCompleted)
            {
                var processes = Process.GetProcessesByName("leap");
                if (processes.Length == 0)
                {
                    var result = await command.Task;
                    throw new InvalidOperationException($"Leap exited after {result.RunTime} with code {result.ExitCode}");
                }
            }

            if (HasTimedOut())
            {
                await this.LogLeapStatus();
                throw new InvalidOperationException($"Max timeout exceeded while waiting for leap to become healthy.");
            }

            await Task.Delay(1000, this._cancellationToken);
        }

        await OnLeapStarted();

        async Task OnLeapStarted()
        {
            // Fetch service URLs
            await Policy.Handle<Exception>()
                .WaitAndRetryAsync(retryCount: 10, i => TimeSpan.FromSeconds(i))
                .ExecuteAsync(this.InitializeDcpResourceUrls);

            if (this._serviceUrls.IsEmpty)
            {
                throw new InvalidOperationException("No service URL found");
            }

            this._logger.LogInformation("Leap started and healthy");
        }
    }

    private async Task KillLeap()
    {
        foreach (var processName in (string[])["Workleap.Leap", "leap", "dcp", "dcpctrl", "dcpproc", "aspire.dashboard"])
        {
            var processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                process.Kill(entireProcessTree: true);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(cts.Token);
            }
        }

        // Killing process hierarchy may take some time, and may not be always successful
        // Check if ports are available for leap, to be sure it will work
        WaitForPortIsAvailable(18887);
        WaitForPortIsAvailable(18888);
        WaitForPortIsAvailable(18889);
    }

    private async Task LogLeapStatus()
    {
        var sb = new StringBuilder();
        var status = await this.CollectAspireStatus();
        if (status is not null)
        {
            sb.AppendLine("# Debug information");
            sb.AppendLine("## Processes");
            foreach (var process in Process.GetProcesses().OrderBy(p => p.ProcessName))
            {
                sb.AppendLine($"- {SafeGetData(() => process.ProcessName)} (pid: {SafeGetData(() => process.Id)}; StartTime: {SafeGetData(() => process.StartTime)})");

                static T? SafeGetData<T>(Func<T> func)
                {
                    try
                    {
                        return func();
                    }
                    catch
                    {
                        return default;
                    }
                }
            }

            sb.AppendLine("");
            sb.AppendLine("## Aspire status");
            sb.AppendLine("Application name: " + status.ApplicationName);
            foreach (var resource in status.Resources.OrderBy(resource => resource.Name, StringComparer.Ordinal))
            {
                sb.AppendLine($"### Resource: {resource.Name} ({resource.Status})");
                sb.AppendLine($"URLs: {string.Join("; ", resource.Endpoints)}");
                foreach (var log in resource.Logs)
                {
                    sb.AppendLine($"  {(log.IsStdErr ? "err: " : "")}{log.Text}");
                }

                sb.AppendLine("\n");
            }

            // List leap certificates and their permissions (investigate permission issue when root-less containers load the certificates)
            if (OperatingSystem.IsLinux())
            {
                sb.AppendLine("## Leap certificates");
                await Cli.Wrap("ls")
                    .WithArguments(["-la", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".leap/generated/certificates/")])
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(line => sb.AppendLine("certificates: " + line)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(line => sb.AppendLine("certificates: " + line)))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteAsync();
            }

            sb.AppendLine("");
            sb.AppendLine("## Environment");
            sb.AppendLine("User: " + Environment.UserName);

            // On CI, we want to collect information about RAM usage. This is useful to understand if the system is running out of memory.
            if (OperatingSystem.IsLinux())
            {
                try
                {
                    var meminfo = await File.ReadAllTextAsync("/proc/meminfo", this._cancellationToken);
                    sb.AppendLine("");
                    sb.AppendLine("## Memory info");
                    sb.AppendLine(meminfo);
                }
                catch
                {
                    // ignored
                }
            }

            this._logger.LogInformation("{LeapStatus}", sb.ToString());
        }
    }

    public void AddConfigurationFiles(params FullPath[] paths)
    {
        foreach (var path in paths)
        {
            if (!File.Exists(path))
            {
                throw new ArgumentException($"File '{path}' does not exist", nameof(paths));
            }

            this._configurationFiles.Add(path);
        }
    }

    public void AddConfigurationFilesFromGitRepository(params string[] gitRepositoryRelativePaths)
    {
        var root = PathHelpers.GetGitRepositoryRoot();
        this.AddConfigurationFiles(gitRepositoryRelativePaths.Select(path => root / path).ToArray());
    }

    public void SetConfigurationFiles(params FullPath[] paths)
    {
        this._configurationFiles.Clear();
        this.AddConfigurationFiles(paths);
    }

    public void SetConfigurationFilesFromGitRepository(params string[] gitRepositoryRelativePaths)
    {
        this._configurationFiles.Clear();
        this.AddConfigurationFilesFromGitRepository(gitRepositoryRelativePaths);
    }

    public void AddHttpHealthCheckUrls(params Func<Uri>[] urls)
    {
        foreach (var url in urls)
        {
            this._healthCheckUris.Add(url);
        }
    }

    public void SetHttpHealthCheckUrls(params Func<Uri>[] urls)
    {
        this._healthCheckUris.Clear();
        this.AddHttpHealthCheckUrls(urls);
    }

    private async Task<bool> IsHealthy()
    {
        var resources = await this.GetAspireResources();
        foreach (var resource in resources)
        {
            if (!resource.IsHealthy)
            {
                return false;
            }
        }

        foreach (var uriFunc in this._healthCheckUris)
        {
            Uri uri;
            try
            {
                uri = uriFunc();
            }
            catch
            {
                await this.InitializeDcpResourceUrls();
                return false;
            }

            if (!await this.IsHealthy(uri))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> IsHealthy(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException($"URL '{uri}' must be absolute");
        }

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
        {
            throw new ArgumentException($"URL '{uri}' must use HTTP or HTTPS");
        }

        try
        {
            using var cts = this.CreateCancellationToken(TimeSpan.FromSeconds(10));
            using var response = await this._httpClient.GetAsync(uri, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            this._logger.LogDebug(ex, "Service '{ServiceUri}' is not healthy", uri);
            return false;
        }
    }

    private static void WaitForPortIsAvailable(int port)
    {
        Policy
            .Handle<Exception>()
            .WaitAndRetry(5, _ => TimeSpan.FromSeconds(2))
            .Execute(() =>
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);

                try
                {
                    listener.Start();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Port '{port}' is not available", ex);
                }
            });
    }

    private GrpcChannel CreateDcpChannel()
    {
        var httpHandler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true,
            KeepAlivePingDelay = TimeSpan.FromSeconds(20),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(10),
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
#pragma warning disable CA5359 // Do Not Disable Certificate Validation
            // If the certificate is not correctly configured in Aspire, we will ignore the certificate validation, so we can still access the logs
            SslOptions = new() { RemoteCertificateValidationCallback = (_, _, _, _) => true },
#pragma warning restore CA5359
        };

        var methodConfig = new MethodConfig
        {
            Names = { MethodName.Default },
            RetryPolicy = new RetryPolicy
            {
                MaxAttempts = 5,
                InitialBackoff = TimeSpan.FromSeconds(1),
                MaxBackoff = TimeSpan.FromSeconds(5),
                BackoffMultiplier = 1.5,
                RetryableStatusCodes = { StatusCode.Unavailable },
            }
        };

        return GrpcChannel.ForAddress("https://localhost:18887",
            channelOptions: new()
            {
                HttpHandler = httpHandler,
                ServiceConfig = new()
                {
                    MethodConfigs =
                    {
                        methodConfig
                    }
                },
                LoggerFactory = this._loggerFactory,
                ThrowOperationCanceledOnCancellation = true,
                DisposeHttpClient = true,
            });
    }

    private async Task InitializeDcpResourceUrls()
    {
        var resources = await this.GetAspireResources();
        foreach (var resource in resources)
        {
            var endpoint = resource.Endpoints.FirstOrDefault();
            if (endpoint is not null)
            {
                this._serviceUrls[resource.Name] = endpoint;
            }
        }
    }

    private async Task<AspireResourceStatus> GetServiceStatus(string leapServiceName)
    {
        var resources = await this.GetAspireResources();
        return resources.Single(r => r.Name == leapServiceName);
    }

    private async Task<bool> IsServiceHealthy(string leapServiceName)
    {
        var status = await this.GetServiceStatus(leapServiceName);
        this._logger.LogDebug("Service status of '{LeapServiceName}': {Status}; IsHealthy: {IsHealthy}", leapServiceName, status.Status, status.IsHealthy);
        return status is { IsHealthy: true, Status: KnownResourceState.Running };
    }

    public async Task StartService(string leapServiceName)
    {
        while (true)
        {
            var status = await this.GetServiceStatus(leapServiceName);

            if (status.CanResourceBeStarted())
            {
                break;
            }

            if (status.Status is KnownResourceState.Starting or KnownResourceState.Running)
            {
                return;
            }

            await Task.Delay(1000, this._cancellationToken);
        }

        await this.ExecuteLifecycleCommand(leapServiceName, KnownResourceCommands.StartCommand);

        while (!await this.IsServiceHealthy(leapServiceName))
        {
            await Task.Delay(1000, this._cancellationToken);
        }
    }

    public async Task StopService(string leapServiceName)
    {
        await this.ExecuteLifecycleCommand(leapServiceName, KnownResourceCommands.StopCommand);

        while (true)
        {
            var status = await this.GetServiceStatus(leapServiceName);
            this._logger.LogDebug("Service status of '{LeapServiceName}': {Status}; IsHealthy: {IsHealthy}", leapServiceName, status.Status, status.IsHealthy);
            if (status.Status is KnownResourceState.Finished or KnownResourceState.NotStarted or KnownResourceState.Exited or KnownResourceState.Unknown)
            {
                return;
            }

            await Task.Delay(1000, this._cancellationToken);
        }
    }

    public async Task RestartService(string leapServiceName)
    {
        await this.ExecuteLifecycleCommand(leapServiceName, KnownResourceCommands.RestartCommand);
        while (!await this.IsServiceHealthy(leapServiceName))
        {
            await Task.Delay(1000, this._cancellationToken);
        }
    }

    private async Task ExecuteLifecycleCommand(string leapServiceName, string commandName)
    {
        var resource = await this.GetServiceStatus(leapServiceName);

        var response = await this._dashboardServiceClient.ExecuteResourceCommandAsync(new ResourceCommandRequest
        {
            ResourceName = resource.ResourceName,
            CommandName = commandName,
            ResourceType = resource.ResourceType,
        }, headers: DcpHeaders, cancellationToken: this._cancellationToken);

        if (response.Kind is not ResourceCommandResponseKind.Succeeded)
        {
            throw new InvalidOperationException($"Cannot start resource '{leapServiceName}': {response.ErrorMessage}");
        }
    }

    private async Task<IReadOnlyCollection<AspireResourceStatus>> GetAspireResources()
    {
        var result = new List<AspireResourceStatus>();

        using var cts = this.CreateCancellationToken(TimeSpan.FromSeconds(5));
        try
        {
            var info = this._dashboardServiceClient.WatchResources(new WatchResourcesRequest()
            {
                IsReconnect = false
            }, headers: DcpHeaders, cancellationToken: cts.Token);
            if (await info.ResponseStream.MoveNext(cts.Token))
            {
                if (info.ResponseStream.Current.KindCase == WatchResourcesUpdate.KindOneofCase.InitialData)
                {
                    foreach (var resource in info.ResponseStream.Current.InitialData.Resources)
                    {
                        if (resource.State == "Hidden")
                        {
                            continue;
                        }

                        var urls = resource.Urls?.Select(url => url.FullUrl).ToArray() ?? [];

                        var isHealthy = resource.HealthReports.Count == 0 || resource.HealthReports.All(report => report.Status is HealthStatus.Healthy);

                        if (!Enum.TryParse<KnownResourceState>(resource.State, out var resourceState))
                        {
                            resourceState = KnownResourceState.Unknown;
                        }

                        // Use display name. Name may contain a hash at the end of the name.
                        var resourceStatus = new AspireResourceStatus(resource.DisplayName, resource.Name, resource.ResourceType, resource.State, resourceState, urls, isHealthy, []);
                        result.Add(resourceStatus);
                    }
                }
            }

            return result;
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cts.Token)
        {
            return result;
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Unauthenticated)
        {
            this._logger.LogError(ex, "err: Cannot get Aspire resources. Be sure to use the latest version of Leap before running the tests.");
            return result;
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "err: Cannot get Aspire resources");
            return result;
        }
    }

    private CancellationTokenSource CreateCancellationToken(TimeSpan delay)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(this._cancellationToken);
        cts.CancelAfter(delay);
        return cts;
    }

    private async Task<AspireStatus?> CollectAspireStatus()
    {
        // Get application info
        using var cts = this.CreateCancellationToken(TimeSpan.FromSeconds(10));
        var applicationInformationResponse = await this._dashboardServiceClient.GetApplicationInformationAsync(new(), headers: DcpHeaders, cancellationToken: cts.Token);
        var result = new AspireStatus(applicationInformationResponse.ApplicationName, Resources: []);

        // List services started in Aspire
        var resources = await this.GetAspireResources();
        foreach (var resource in resources)
        {
            result.Resources.Add(resource);
        }

        // Get logs for each services
        foreach (var resource in result.Resources)
        {
            if (resource.ResourceType is "Remote")
            {
                continue;
            }

            try
            {
                var log = this._dashboardServiceClient.WatchResourceConsoleLogs(new()
                {
                    ResourceName = resource.Name
                }, headers: DcpHeaders, cancellationToken: this._cancellationToken);
                var logs = await this.EnumerateAsync(log);
                foreach (var item in logs.SelectMany(log => log.LogLines))
                {
                    resource.Logs.Add(new LogEntry(item.Text, item.IsStdErr, item.LineNumber));
                }
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "err: Cannot get logs of Aspire resource {ResourceName}", resource.Name);
            }
        }

        return result;
    }

    private async Task<List<T>> EnumerateAsync<T>(AsyncServerStreamingCall<T> response)
    {
        // We are not interested in live streaming the logs. We just need all the logs until now.
        // To stop waiting for new logs we will cancel the request after 500ms
        var result = new List<T>();
        while (await TryMoveNext(response))
        {
            result.Add(response.ResponseStream.Current);
        }

        return result;

        async Task<bool> TryMoveNext(AsyncServerStreamingCall<T> response)
        {
            using var cts = this.CreateCancellationToken(TimeSpan.FromMilliseconds(500));
            try
            {
                return await response.ResponseStream.MoveNext(cts.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }

    private sealed record AspireStatus(string ApplicationName, List<AspireResourceStatus> Resources);

    private sealed record AspireResourceStatus(string Name, string ResourceName, string ResourceType, string RawStatus, KnownResourceState Status, string[] Endpoints, bool IsHealthy, List<LogEntry> Logs)
    {
        public bool CanResourceBeStarted()
        {
            return this.Status is KnownResourceState.Finished or KnownResourceState.Exited or KnownResourceState.FailedToStart or KnownResourceState.NotStarted;
        }
    }

    private sealed record LogEntry(string Text, bool IsStdErr, int LineNumber);
}
