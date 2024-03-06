using System.CommandLine.Parsing;

namespace Leap.Cli.Platform;

internal static class ParseResultExtensions
{
    internal static string GetFullCommandName(this ParseResult parseResult)
    {
        var commandNames = new List<string>();

        var commandResult = parseResult.CommandResult;

        while (commandResult != null && commandResult != parseResult.RootCommandResult)
        {
            commandNames.Add(commandResult.Command.Name);
            commandResult = commandResult.Parent as CommandResult;
        }

        commandNames.Reverse();

        return string.Join(' ', commandNames);
    }
}