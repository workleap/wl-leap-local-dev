using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.IO.Abstractions;
using Leap.Cli.Commands;
using Leap.Cli.Platform;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

var rootCommand = new RootCommand("Workleap's Local Environment Application Proxy")
{
    new AboutCommand(),
    new RunCommand(),
};

rootCommand.Name = "leap";

var builder = new CommandLineBuilder(rootCommand);

builder.UseDefaults();
builder.UseDependencyInjection(services =>
{
    services.AddSingleton(AnsiConsole.Console);
    services.AddSingleton<IFileSystem, FileSystem>();
});

var parser = builder.Build();
return parser.Invoke(args, new Utf8SystemConsole());