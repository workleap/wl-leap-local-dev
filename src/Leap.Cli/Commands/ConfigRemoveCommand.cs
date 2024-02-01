using System.CommandLine;
using Leap.Cli.Configuration;
using Leap.Cli.Platform;

namespace Leap.Cli.Commands;

internal sealed class ConfigRemoveCommand : Command<ConfigRemoveCommandOptions, ConfigRemoveCommandHandler>
{
    private static readonly Argument<string> PathArgument = new Argument<string>("path")
    {
        Description = "Path to the Leap YAML file.",
        Arity = ArgumentArity.ExactlyOne,
    }.LegalFilePathsOnly();

    public ConfigRemoveCommand()
        : base("remove", "Removes a file from the list of Leap YAML files to be used by Leap.")
    {
        this.AddArgument(PathArgument);
    }
}

internal sealed class ConfigRemoveCommandOptions : ICommandOptions
{
    public string Path { get; init; } = string.Empty;
}

internal sealed class ConfigRemoveCommandHandler(IUserSettingsManager userSettingsManager)
    : ICommandOptionsHandler<ConfigRemoveCommandOptions>
{
    public async Task<int> HandleAsync(ConfigRemoveCommandOptions options, CancellationToken cancellationToken)
    {
        // TODO error handling
        await userSettingsManager.RemoveLeapYamlFilePathAsync(options.Path, cancellationToken);
        return 0;
    }
}