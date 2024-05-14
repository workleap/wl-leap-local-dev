using System.CommandLine;
using Leap.Cli.Configuration;
using Leap.Cli.Platform;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform.Telemetry;

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

        this.AddOption(fileOption);
    }
}

internal sealed class RunCommandOptions : ICommandOptions
{
    public string[] File { get; init; } = [];
}

internal sealed class RunCommandHandler(LeapPipeline pipeline, LeapConfigManager leapConfigManager)
    : ICommandOptionsHandler<RunCommandOptions>
{
    public async Task<int> HandleAsync(RunCommandOptions runCommandOptions, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackLeapRun();
        leapConfigManager.SetConfigurationFilesAsync(runCommandOptions.File);

        await pipeline.RunAsync(cancellationToken);

        return 0;
    }
}