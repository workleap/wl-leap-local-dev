using System.CommandLine;
using System.CommandLine.Parsing;
using Leap.Cli.Configuration;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform;
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

        var remoteEnvOption = new Option<string?>("--remote-env", parseArgument: ParseRemoteEnvArgument)
        {
            Description = "The remote environment for Leap services",
            Arity = ArgumentArity.ZeroOrOne,
            IsRequired = false
        };

        var startServicesExplicitlyOption = new Option<bool>("--start-services-explicitly")
        {
            Description = "Let users start services explicitly using the dashboard",
            Arity = ArgumentArity.ZeroOrOne,
            IsRequired = false
        };

        this.AddOption(fileOption);
        this.AddOption(remoteEnvOption);
        this.AddOption(startServicesExplicitlyOption);
    }

    private static string? ParseRemoteEnvArgument(ArgumentResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return "";
        }

        return result.Tokens[0].Value;
    }
}

internal sealed class RunCommandOptions : ICommandOptions
{
    public string[] File { get; init; } = [];
    public string? RemoteEnv { get; init; }
    public bool StartServicesExplicitly { get; init; }
}

internal sealed class RunCommandHandler(LeapPipeline pipeline, LeapConfigManager leapConfigManager)
    : ICommandOptionsHandler<RunCommandOptions>
{
    public async Task<int> HandleAsync(RunCommandOptions runCommandOptions, CancellationToken cancellationToken)
    {
        TelemetryMeters.TrackLeapRun();
        leapConfigManager.SetConfigurationFilesAsync(runCommandOptions.File);

        leapConfigManager.SetEnvironmentName(runCommandOptions.RemoteEnv);
        leapConfigManager.SetStartServicesExplicitly(runCommandOptions.StartServicesExplicitly);

        await pipeline.RunAsync(cancellationToken);

        return 0;
    }
}