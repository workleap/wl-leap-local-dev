using Leap.Cli.Platform;
using Spectre.Console;

namespace Leap.Cli.Commands;

internal sealed class RunCommand : Command<RunCommandOptions, RunCommandOptionsHandler>
{
    public RunCommand()
        : base("run", "Run Leap")
    {
    }
}

internal sealed class RunCommandOptions : ICommandOptions
{
}

internal sealed class RunCommandOptionsHandler : ICommandOptionsHandler<RunCommandOptions>
{
    private readonly IAnsiConsole _console;

    public RunCommandOptionsHandler(IAnsiConsole console)
    {
        this._console = console;
    }

    public Task<int> HandleAsync(RunCommandOptions options, CancellationToken cancellationToken)
    {
        this._console.WriteLine("Hello world");
        return Task.FromResult(0);
    }
}