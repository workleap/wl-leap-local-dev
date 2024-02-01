using Leap.Cli.Platform;
using Leap.Cli.Pipeline;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Commands;

internal sealed class RunCommand() : Command<RunCommandOptions, RunCommandHandler>("run", "Run Leap");

internal sealed class RunCommandOptions : ICommandOptions;

internal sealed class RunCommandHandler(IEnumerable<IPipelineStep> pipelineSteps, ILoggerFactory loggerFactory)
    : ICommandOptionsHandler<RunCommandOptions>
{
    private readonly LeapPipeline _pipeline = new(pipelineSteps, loggerFactory);

    public async Task<int> HandleAsync(RunCommandOptions runCommandOptions, CancellationToken cancellationToken)
    {
        await this._pipeline.RunAsync(cancellationToken);
        return 0;
    }
}