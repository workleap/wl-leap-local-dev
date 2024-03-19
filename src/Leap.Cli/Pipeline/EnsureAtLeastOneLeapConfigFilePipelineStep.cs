using Leap.Cli.Configuration;
using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class EnsureAtLeastOneLeapConfigFilePipelineStep : IPipelineStep
{
    private readonly ILeapYamlAccessor _leapYamlAccessor;

    public EnsureAtLeastOneLeapConfigFilePipelineStep(ILeapYamlAccessor leapYamlAccessor)
    {
        this._leapYamlAccessor = leapYamlAccessor;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var leapConfigs = await this._leapYamlAccessor.GetAllAsync(cancellationToken);

        if (leapConfigs.Length == 0)
        {
            throw new LeapException("No config file found, provide a leap.yaml file in the current directory or use the --file argument.");
        }
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}