using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal interface IPipelineStep
{
    Task StartAsync(ApplicationState state, CancellationToken cancellationToken);

    Task StopAsync(ApplicationState state, CancellationToken cancellationToken);
}