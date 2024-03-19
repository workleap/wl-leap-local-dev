using System.CommandLine;
using Leap.Cli.Configuration;
using Leap.Cli.Platform;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Commands;

internal sealed class RunCommand : Command<RunCommandOptions, RunCommandHandler>
{
    public RunCommand() : base("run", "Run Leap")
    {
        var fileOption = new Option<string[]>(["-f", "--file"])
        {
            AllowMultipleArgumentsPerToken = true,
            Description = "One or many leap.yaml configuration files to run.",
            Arity = ArgumentArity.ZeroOrMore,
            IsRequired = false
        };
        var diagnosticOption = new Option<bool>(["--diagnostic"]) { Description = "Enable diagnostic mode.", Arity = ArgumentArity.ZeroOrOne, IsHidden = true };

        this.AddOption(fileOption);
        this.AddOption(diagnosticOption);
    }
}

internal sealed class RunCommandOptions : ICommandOptions
{
    public string[] File { get; init; } = Array.Empty<string>();
    public bool Diagnostic { get; init; }
}

internal sealed class RunCommandHandler(IEnumerable<IPipelineStep> pipelineSteps, LeapConfigManager leapConfigManager, ITelemetryHelper telemetryHelper, ILoggerFactory loggerFactory)
    : ICommandOptionsHandler<RunCommandOptions>
{
    private LeapPipeline? _pipeline;

    public async Task<int> HandleAsync(RunCommandOptions runCommandOptions, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackLeapRun();
        leapConfigManager.SetConfigurationFilesAsync(runCommandOptions.File);

        this._pipeline = new LeapPipeline(pipelineSteps, telemetryHelper, loggerFactory, runCommandOptions.Diagnostic);
        await this._pipeline.RunAsync(cancellationToken);

        return 0;
    }
}