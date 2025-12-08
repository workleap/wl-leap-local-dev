using Leap.Cli.Aspire;
using Leap.Cli.Model;
using Leap.Cli.Platform.Telemetry;

namespace Leap.Cli.Pipeline;

internal sealed class StartAspirePipelineStep(IAspireManager aspireManager) : IPipelineStep
{
    private DistributedApplication? _app;

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        this._app = await aspireManager.StartAppHostAsync(cancellationToken);
        var runnerDistribution = GetRunnerDistribution(state);
        TelemetryMeters.TrackNumberServices(state.Services.Count, runnerDistribution);
    }

    public async Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        if (this._app != null)
        {
            await this._app.StopAsync(cancellationToken);
            await this._app.DisposeAsync();
        }
    }

    private static Dictionary<string, int> GetRunnerDistribution(ApplicationState state)
    {
        var runnerCounts = new Dictionary<string, int>
        {
            { Constants.DotnetRunnerYamlDiscriminator, 0 },
            { Constants.DockerRunnerYamlDiscriminator, 0 },
            { Constants.ExecutableRunnerYamlDiscriminator, 0 },
            { Constants.OpenApiRunnerYamlDiscriminator, 0 },
            { Constants.RemoteRunnerYamlDiscriminator, 0 },
        };

        foreach (var (_, service) in state.Services)
        {
            if (runnerCounts.TryGetValue(service.ActiveRunner.Type, out var count))
            {
                runnerCounts[service.ActiveRunner.Type] = count + 1;
            }
        }

        return runnerCounts;
    }
}