using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.IO.Abstractions;
using Leap.Cli.Commands;
using Leap.Cli.Platform;
using Leap.Cli.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

var rootCommand = new RootCommand("Workleap's Local Environment Application Proxy")
{
    new AboutCommand(),
    new RunCommand(),
};

rootCommand.Name = "leap";

var builder = new CommandLineBuilder(rootCommand);
var console = new RecordingConsole();

builder.UseDefaults();

builder.UseDependencyInjection(services =>
{
    services.AddSingleton<IRecordingConsole>(console);
    services.AddSingleton<IConsole>(console);
    services.AddSingleton<IAnsiConsole>(console);

    services.AddSingleton<IFileSystem, FileSystem>();
});

var parser = builder.Build();
return parser.Invoke(args, console);