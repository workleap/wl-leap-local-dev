using Leap.Cli.Configuration;
using Leap.Cli.Configuration.Yaml;
using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Packaging;

namespace Leap.Cli.Aspire;

internal static class ResourceBuilderExtensions
{
    public static IResourceBuilder<T> WithEnvironment<T>(this IResourceBuilder<T> builder, IEnumerable<KeyValuePair<string, string>> environmentVariables)
        where T : IResourceWithEnvironment
    {
        return builder.WithAnnotation(new EnvironmentCallbackAnnotation(context =>
        {
            foreach (var (key, value) in environmentVariables)
            {
                context.EnvironmentVariables[key] = value;
            }
        }));
    }

    public static IResourceBuilder<T> WithConfigurePreferredRunnerCommand<T>(this IResourceBuilder<T> builder, Service service)
        where T : IResource, IResourceWithWaitSupport
    {
        var commands = GetPreferredRunnerCommands(builder.Resource, service);
        builder.Resource.Annotations.AddRange(commands);
        return builder;
    }

    private static IEnumerable<ResourceCommandAnnotation> GetPreferredRunnerCommands(IResource resource, Service service)
    {
        var runnerNames = service.GetRunnerNames();
        if (runnerNames.Count == 1)
        {
            return [];
        }

        return runnerNames
            .Select(runner => GetConfigureRunnerCommand(resource, service, runner));
    }

    private static ResourceCommandAnnotation GetConfigureRunnerCommand(IResource resource, Service service, string runnerName)
    {
        return new ResourceCommandAnnotation(
            name: $"configure-{runnerName}-runner",
            displayName: runnerName switch
            {
                DockerRunnerYaml.YamlDiscriminator => "Run service using Docker",
                DotnetRunnerYaml.YamlDiscriminator => "Run service with .NET and support for an IDE",
                ExecutableRunnerYaml.YamlDiscriminator => "Run service as an executable",
                OpenApiRunnerYaml.YamlDiscriminator => "Run service as an OpenAPI mock",
                RemoteRunnerYaml.YamlDiscriminator => "Use a remote service running in the cloud",
                _ => $"Set {runnerName} as preferred runner"
            },
            updateState: _ => string.IsNullOrEmpty(service.PreferredRunner) && runnerName == service.ActiveRunner.Type
                ? ResourceCommandState.Disabled
                : runnerName == service.PreferredRunner
                    ? ResourceCommandState.Disabled
                    : ResourceCommandState.Enabled,
            executeCommand: async context =>
            {
                TelemetryMeters.TrackPreferencesCommand();
                try
                {
                    var preferencesSettingsManager = context.ServiceProvider.GetRequiredService<PreferencesSettingsManager>();
                    await preferencesSettingsManager.SetPreferredRunnerForServiceAsync(service.Name, runnerName, context.CancellationToken);
                    service.PreferredRunner = runnerName;
                }
                catch (Exception ex)
                {
                    return new ExecuteCommandResult { ErrorMessage = "An error occurred while setting a preferred runner for resource: " + ex.Message, Success = false };
                }
                finally
                {
                    await context.TriggerResourceSnapshotChangeAsync(resource);
                }

                return CommandResults.Success();
            },
            displayDescription: null,
            parameter: null,
            confirmationMessage: $"You must restart Leap local dev for the runner preference change to {service.Name} service to take effect.",
            iconName: "PlaySettings",
            iconVariant: IconVariant.Filled,
            isHighlighted: false);
    }
}