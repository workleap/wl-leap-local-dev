using Leap.Cli.Config;
using Leap.Cli.Platform;
using Spectre.Console;

namespace Leap.Cli.Commands;

internal sealed class GenerateCommand : Command<GenerateCommand.Options, GenerateCommand.OptionsHandler>
{
    public GenerateCommand() : base("generate", "Generate Yaml code model from JSON Schema")
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
            this._console.WriteLine("Generating code...");
            await CodeGenerator.Generate("./Yaml/leap-spec.json", "Leap");
            return await Task.FromResult(0);
        }
    }
}