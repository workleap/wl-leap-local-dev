using System.CommandLine;
using Leap.Cli.Platform;

namespace Leap.Cli.Commands;

internal sealed class UpdateHostsFileCommand : Command<UpdateHostsFileCommandOptions, UpdateHostsFileCommandHandler>
{
    public const char HostSeparator = ';';
    public const string CommandName = "updatehostsfile";

    private static readonly Argument<string> PathArgument = new Argument<string>("hosts")
    {
        Description = "Semicolon-separated list of hostnames to add to the hosts file.",
        Arity = ArgumentArity.ExactlyOne,
    };

    public UpdateHostsFileCommand()
        : base(CommandName, "Registers a Leap YAML file to be used by Leap.")
    {
        // Internal command, not meant to be used directly
        this.IsHidden = true;

        this.AddArgument(PathArgument);
    }
}

internal sealed class UpdateHostsFileCommandOptions : ICommandOptions
{
    public string Hosts { get; init; } = string.Empty;
}

internal sealed class UpdateHostsFileCommandHandler(IPlatformHelper platformHelper, IHostsFileManager hostsFileManager)
    : ICommandOptionsHandler<UpdateHostsFileCommandOptions>
{
    public async Task<int> HandleAsync(UpdateHostsFileCommandOptions options, CancellationToken cancellationToken)
    {
        if (!platformHelper.IsCurrentProcessElevated)
        {
            return 1;
        }

        var hosts = options.Hosts.Split(UpdateHostsFileCommand.HostSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        await hostsFileManager.UpdateLeapManagedHostnamesAsync(hosts, cancellationToken);

        // Consider signaling the other process through a named pipe?
        return 0;
    }
}