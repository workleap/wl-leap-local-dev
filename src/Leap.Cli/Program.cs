using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.IO.Abstractions;
using Leap.Cli.Commands;
using Leap.Cli.Configuration;
using Leap.Cli.Dependencies;
using Leap.Cli.DockerCompose;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    services.AddSingleton<ICliWrapCommandExecutor, CliWrapCommandExecutor>();

    services.AddSingleton<ILeapYamlAccessor, LeapYamlAccessor>();
    services.AddSingleton<IDependencyHandler, MongoDependencyHandler>();
    services.AddSingleton<IDependencyYamlHandler, MongoDependencyYamlHandler>();

    services.AddSingleton<DockerComposeManager>();
    services.AddSingleton<IConfigureDockerCompose>(x => x.GetRequiredService<DockerComposeManager>());
    services.AddSingleton<IDockerComposeManager>(x => x.GetRequiredService<DockerComposeManager>());

    services.TryAddEnumerable(new[]
    {
        ServiceDescriptor.Singleton<IPipelineStep, EnsureLeapDirectoryCreatedPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, PopulateDependenciesPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, BeforeStartingDependenciesPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, StartDockerComposePipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, AfterStartingDependenciesPipelineStep>(),
    });
});

var parser = builder.Build();
return parser.Invoke(args, new Utf8SystemConsole());