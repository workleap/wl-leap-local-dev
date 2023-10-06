using Leap.Cli.Config;
using Leap.Cli.Platform;
using Spectre.Console;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Leap.Cli.Commands;

internal sealed class ReadCommand : Command<ReadCommand.Options, ReadCommand.OptionsHandler>
{
    public ReadCommand() : base("read", "Read leap.yml file")
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

        public async Task<int> HandleAsync(Options options, CancellationToken cancellationToken)
        {
            this._console.WriteLine("Reading leap.yml...");

            var serializer = new LeapConfigSerializer();

            using var reader = File.OpenText("leap.yml");
            var leap = serializer.Deserialize(reader.BaseStream);
            await using var writer = Console.OpenStandardOutput();
            serializer.Serialize(writer, leap);

            return await Task.FromResult(0);
        }
    }
}