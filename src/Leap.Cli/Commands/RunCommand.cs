using System.CommandLine;
using Leap.Cli.Platform;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Commands;

internal sealed class RunCommand : Command<RunCommandOptions, RunCommandHandler>
{
    public RunCommand() : base("run", "Run Leap")
    {
        var diagnosticOption = new Option<bool>(["--diagnostic"]) { Description = "Enable diagnostic mode.", Arity = ArgumentArity.ZeroOrOne, IsHidden = true };
        this.AddOption(diagnosticOption);
    }
}

internal sealed class RunCommandOptions : ICommandOptions
{
    public bool Diagnostic { get; init; }
}

internal sealed class RunCommandHandler(IEnumerable<IPipelineStep> pipelineSteps, ITelemetryHelper telemetryHelper, ILoggerFactory loggerFactory)
    : ICommandOptionsHandler<RunCommandOptions>
{
    private LeapPipeline? _pipeline;

    public async Task<int> HandleAsync(RunCommandOptions runCommandOptions, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackLeapRun();
        this._pipeline = new LeapPipeline(pipelineSteps, telemetryHelper, loggerFactory, runCommandOptions.Diagnostic);
        await this._pipeline.RunAsync(cancellationToken);
        return 0;
    }
}