using Leap.Cli.Platform;
using Spectre.Console;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Leap.Cli.Commands;

internal sealed class AboutCommand : Command<AboutCommand.Options, AboutCommand.OptionsHandler>
{
    public AboutCommand() : base("about", "About")
    {
    }

    internal sealed class Options : ICommandOptions
    {
    }

    internal sealed class OptionsHandler : ICommandOptionsHandler<Options>
    {
        private readonly IAnsiConsole _console;

        public OptionsHandler(IAnsiConsole console)
        {
            this._console = console;
        }

        public Task<int> HandleAsync(Options options, CancellationToken cancellationToken)
        {
            this._console.WriteLine("About");
            return Task.FromResult(0);
        }
    }
}