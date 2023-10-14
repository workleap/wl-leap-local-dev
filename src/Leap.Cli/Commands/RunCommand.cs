using Leap.Cli.Platform;
using Leap.Cli.Pipeline;

namespace Leap.Cli.Commands;

internal sealed class RunCommand : Command<RunCommand.Options, RunCommand.OptionsHandler>
{
    public RunCommand()
        : base("run", "Run Leap")
    {
    }

    internal sealed class Options : ICommandOptions
    {
    }

    internal sealed class OptionsHandler : ICommandOptionsHandler<Options>
    {
        private readonly LeapPipeline _pipeline;

        public OptionsHandler(IEnumerable<IPipelineStep> pipelineSteps)
        {
            this._pipeline = new LeapPipeline(pipelineSteps);
        }

        public async Task<int> HandleAsync(Options options, CancellationToken cancellationToken)
        {
            await this._pipeline.RunAsync(cancellationToken);
            return 0;
        }
    }
}