using System.CommandLine;
using Leap.Cli.Configuration;
using Leap.Cli.Platform;

namespace Leap.Cli.Commands;

internal sealed class ConfigAddCommand : Command<ConfigAddCommandOptions, ConfigAddCommandHandler>
{
    private static readonly Argument<string> PathArgument = new Argument<string>("path")
    {
        Description = "Path to the Leap YAML file.",
        Arity = ArgumentArity.ExactlyOne,
    }.LegalFilePathsOnly();

    public ConfigAddCommand()
        : base("add", "Registers a Leap YAML file to be used by Leap.")
    {
        this.AddArgument(PathArgument);
    }
}

internal sealed class ConfigAddCommandOptions : ICommandOptions
{
    public string Path { get; init; } = string.Empty;
}

internal sealed class ConfigAddCommandHandler(IUserSettingsManager userSettingsManager)
    : ICommandOptionsHandler<ConfigAddCommandOptions>
{
    public async Task<int> HandleAsync(ConfigAddCommandOptions options, CancellationToken cancellationToken)
    {
        // TODO error handling
        await userSettingsManager.AddLeapYamlFilePathAsync(options.Path, cancellationToken);
        return 0;
    }
}