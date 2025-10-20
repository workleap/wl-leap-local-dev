using System.Collections.Concurrent;
using System.Reflection;
using Aspire.Hosting.Lifecycle;
using Leap.Cli.Platform.Telemetry;
using Leap.StartupHook;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Aspire;

internal sealed class DotnetExecutableResource(string name, string command, string workingDirectory)
    : ExecutableResource(name, command, workingDirectory)
{
    public string DebuggingSignalFilePath { get; } = Path.Combine(Constants.DotnetExecutableDebuggingDirectoryPath, Guid.NewGuid().ToString());
}

internal static class DotnetExecutableResourceExtensions
{
    public static IResourceBuilder<DotnetExecutableResource> AddDotnetExecutable(
        this IDistributedApplicationBuilder builder, [ResourceName] string name, string workingDirectory, string projectPath, bool watch)
    {
        // dotnet watch arguments inspired by .NET Aspire:
        // https://github.com/dotnet/aspire/blob/v8.0.1/src/Aspire.Hosting/Dcp/ApplicationExecutor.cs#L1004-L1022
        string[] args = watch
            ? ["watch", "--project", projectPath, "--no-launch-profile", "--non-interactive", "--no-hot-reload"]
            : ["run", "--project", projectPath, "--no-launch-profile"];

        var resource = new DotnetExecutableResource(name, "dotnet", workingDirectory);

        return builder.AddResource(resource).WithArgs(args);
    }

    public static IResourceBuilder<DotnetExecutableResource> WithRestartAndWaitForDebuggerCommand(this IResourceBuilder<DotnetExecutableResource> builder)
    {
        builder.ApplicationBuilder.Services.TryAddLifecycleHook<DotnetExecutableLifecycleHook>();

        // .NET hooks are executed before the application starts. Inspired by:
        // https://github.com/dotnet/tye/blob/release/0.11.0/src/Microsoft.Tye.Hosting/ProcessRunner.cs#L223
        // See also: https://github.com/dotnet/runtime/blob/v8.0.10/docs/design/features/host-startup-hook.md
        return builder.WithEnvironment(context =>
        {
            context.EnvironmentVariables["DOTNET_STARTUP_HOOKS"] = typeof(StartupHookAssemblyHandle).Assembly.Location;
            context.EnvironmentVariables["LEAP_SERVICE_NAME"] = builder.Resource.Name;
            context.EnvironmentVariables["LEAP_STARTUP_HOOK_SIGNAL_FILE_PATH"] = builder.Resource.DebuggingSignalFilePath;

            // Reduce build time to improve feedback loop
            context.EnvironmentVariables["RunAnalyzers"] = "false";
            context.EnvironmentVariables["NuGetAudit"] = "false";
        });
    }

    private sealed class DotnetExecutableLifecycleHook : IDistributedApplicationLifecycleHook
    {
        private readonly ConcurrentDictionary<string, bool> _areResourcesRestartingForDebugging = new(StringComparer.OrdinalIgnoreCase);
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DotnetExecutableLifecycleHook> _logger;

        private readonly Type _applicationOrchestratorType;
        private readonly MethodInfo _startResourceAsyncMethodInfo;
        private readonly MethodInfo _stopResourceAsyncMethodInfo;

        private object? _applicationExecutor;

        public DotnetExecutableLifecycleHook(IServiceProvider serviceProvider, ILogger<DotnetExecutableLifecycleHook> logger)
        {
            this._serviceProvider = serviceProvider;
            this._logger = logger;

            const string typeName = "Aspire.Hosting.Orchestrator.ApplicationOrchestrator";
            try
            {
                this._applicationOrchestratorType = typeof(DistributedApplication).Assembly.GetType(typeName)
                    ?? throw new InvalidOperationException($"Type '{typeName}' not found, check if it still exists in .NET Aspire's recent code: https://github.com/dotnet/aspire/blob/8896123261cb54a75cef50b3579be067ccc8bf73/src/Aspire.Hosting/Orchestrator/ApplicationOrchestrator.cs#L15");

                this._startResourceAsyncMethodInfo = this._applicationOrchestratorType.GetMethod("StartResourceAsync", BindingFlags.Public | BindingFlags.Instance, [typeof(string), typeof(CancellationToken)])
                    ?? throw new InvalidOperationException($"Method 'StartResourceAsync' not found on type '{typeName}', check if it still exists in .NET Aspire's recent code: https://github.com/dotnet/aspire/blob/8896123261cb54a75cef50b3579be067ccc8bf73/src/Aspire.Hosting/Orchestrator/ApplicationOrchestrator.cs#L195");

                this._stopResourceAsyncMethodInfo = this._applicationOrchestratorType.GetMethod("StopResourceAsync", BindingFlags.Public | BindingFlags.Instance, [typeof(string), typeof(CancellationToken)])
                    ?? throw new InvalidOperationException($"Method 'StopResourceAsync' not found on type '{typeName}', check if it still exists in .NET Aspire's recent code: https://github.com/dotnet/aspire/blob/8896123261cb54a75cef50b3579be067ccc8bf73/src/Aspire.Hosting/Orchestrator/ApplicationOrchestrator.cs#L224");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"An error occurred while trying to reflect on the '{typeName}' type");
                throw;
            }
        }

        private async Task RestartResourceAsync(string resourceName, CancellationToken cancellationToken)
        {
            try
            {
                // We copied the "Restart" logic from the built-in "Restart" command:
                // https://github.com/dotnet/aspire/blob/v9.1.0/src/Aspire.Hosting/ApplicationModel/CommandsConfigurationExtensions.cs#L92-L93
                await (Task)this._stopResourceAsyncMethodInfo.Invoke(this._applicationExecutor, [resourceName, cancellationToken])!;
                await (Task)this._startResourceAsyncMethodInfo.Invoke(this._applicationExecutor, [resourceName, cancellationToken])!;
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "An error occurred while trying to restart the resource '{ResourceName}'", resourceName);
            }
        }

        public Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
        {
            this._applicationExecutor = this._serviceProvider.GetRequiredService(this._applicationOrchestratorType);

            foreach (var dotnetExecutable in appModel.Resources.OfType<DotnetExecutableResource>())
            {
                this._areResourcesRestartingForDebugging[dotnetExecutable.Name] = false;
                this.AddRestartAndWaitForDebuggerCommand(dotnetExecutable);
            }

            return Task.CompletedTask;
        }

        private void AddRestartAndWaitForDebuggerCommand(DotnetExecutableResource resource)
        {
            var command = new ResourceCommandAnnotation(
                name: "dotnet-restart-and-debug",
                displayName: "Restart and wait for debugger",
                updateState: context =>
                {
                    if (KnownResourceStates.TerminalStates.Contains(context.ResourceSnapshot.State?.Text))
                    {
                        return ResourceCommandState.Disabled;
                    }

                    return this._areResourcesRestartingForDebugging.GetValueOrDefault(resource.Name)
                        ? ResourceCommandState.Disabled
                        : ResourceCommandState.Enabled;
                },
                executeCommand: async context =>
                {
                    if (!this._areResourcesRestartingForDebugging.TryUpdate(resource.Name, newValue: true, comparisonValue: false))
                    {
                        return new ExecuteCommandResult
                        {
                            ErrorMessage = "Another restart operation is already in progress for this resource.",
                            Success = false
                        };
                    }

                    try
                    {
                        await context.TriggerResourceSnapshotChangeAsync(resource);

                        PersistentDotnetExecutableDebuggingState.EnableDebugging(resource.DebuggingSignalFilePath);

                        // .NET Aspire adds a suffix to executable resource names
                        var resourceId = context.ResourceName;
                        await this.RestartResourceAsync(resourceId, context.CancellationToken);
                    }
                    catch (Exception ex)
                    {
                        return new ExecuteCommandResult
                        {
                            ErrorMessage = "An error occurred while trying to restart the resource with debugging enabled: " + ex.Message,
                            Success = false
                        };
                    }
                    finally
                    {
                        this._areResourcesRestartingForDebugging[resource.Name] = false;
                        await context.TriggerResourceSnapshotChangeAsync(resource);
                    }

                    TelemetryMeters.TrackWaitForDebuggerCommand(resource.Name);
                    return CommandResults.Success();
                },
                displayDescription: null,
                parameter: null,
                confirmationMessage: null,
                iconName: "BugArrowCounterclockwise",
                iconVariant: IconVariant.Filled,
                isHighlighted: false);

            resource.Annotations.Add(command);
        }
    }
}