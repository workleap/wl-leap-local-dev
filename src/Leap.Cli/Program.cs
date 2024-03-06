using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.IO.Abstractions;
using Leap.Cli.Aspire;
using Leap.Cli.Commands;
using Leap.Cli.Configuration;
using Leap.Cli.Dependencies;
using Leap.Cli.Dependencies.Azurite;
using Leap.Cli.DockerCompose;
using Leap.Cli.Model;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

ConsoleDefaults.SetInvariantCulture();
ConsoleDefaults.SetUtf8Encoding();

var quietOption = new Option<bool>(["--quiet"])
{
    Description = "Hide debugging messages.",
    Arity = ArgumentArity.ZeroOrOne,
    IsHidden = true,
};

var featureFlagsOption = new Option<string[]>(["--feature-flags"])
{
    AllowMultipleArgumentsPerToken = true,
    Description = "Enable feature flags.",
    Arity = ArgumentArity.ZeroOrMore,
    IsHidden = true,
};

var rootCommand = new RootCommand("Workleap's Local Environment Application Proxy")
{
    new RunCommand(),
    new ConfigCommand
    {
        new ConfigAddCommand(),
        new ConfigRemoveCommand(),
    },
    new UpdateHostsFileCommand(),
};

rootCommand.AddGlobalOption(quietOption);
rootCommand.AddGlobalOption(featureFlagsOption);
rootCommand.Name = "leap";

var builder = new CommandLineBuilder(rootCommand);

builder.UseDefaults();
builder.UseDependencyInjection((services, context) =>
{
    services.AddLogging(loggingBuilder =>
    {
        var isQuiet = context.ParseResult.GetValueForOption(quietOption);

        loggingBuilder.AddFilter(nameof(Leap), isQuiet ? LogLevel.Information : LogLevel.Trace);
        loggingBuilder.AddFilter("System.Net.Http", LogLevel.Warning);
        loggingBuilder.AddProvider(new SimpleColoredConsoleLoggerProvider());
    });

    var experimentsCommandLine = context.ParseResult.GetValueForOption(featureFlagsOption) ?? [];
    services.AddSingleton<IFeatureManager>(new FeatureManager(experimentsCommandLine));

    services.AddSingleton<IFileSystem, FileSystem>();
    services.AddSingleton<ICliWrap, CliWrapExecutor>();
    services.AddSingleton<IPlatformHelper, PlatformHelper>();
    services.AddSingleton<IPortManager, PortManager>();
    services.AddSingleton<IHostsFileManager, HostsFileManager>();
    services.AddSingleton<ITelemetryHelper, TelemetryHelper>();
    services.AddSingleton<AzuriteAuthenticationHandler>();

    services.AddHttpClient()
        .AddHttpClient(AzuriteConstants.HttpClientName)
        .AddHttpMessageHandler<AzuriteAuthenticationHandler>();

    services.AddSingleton<ILeapYamlAccessor, LeapYamlAccessor>();

    services.AddSingleton<IDependencyHandler, MongoDependencyHandler>();
    services.AddSingleton<IDependencyHandler, RedisDependencyHandler>();
    services.AddSingleton<IDependencyHandler, AzuriteDependencyHandler>();
    services.AddSingleton<IDependencyHandler, SqlServerDependencyHandler>();
    services.AddSingleton<IDependencyHandler, PostgresDependencyHandler>();
    services.AddSingleton<IDependencyHandler, EventGridDependencyHandler>();

    services.AddSingleton<IDependencyYamlHandler, MongoDependencyYamlHandler>();
    services.AddSingleton<IDependencyYamlHandler, RedisDependencyYamlHandler>();
    services.AddSingleton<IDependencyYamlHandler, AzuriteDependencyYamlHandler>();
    services.AddSingleton<IDependencyYamlHandler, SqlServerDependencyYamlHandler>();
    services.AddSingleton<IDependencyYamlHandler, PostgresDependencyYamlHandler>();
    services.AddSingleton<IDependencyYamlHandler, EventGridDependencyYamlHandler>();

    services.AddSingleton<DockerComposeManager>();
    services.AddSingleton<IConfigureDockerCompose>(x => x.GetRequiredService<DockerComposeManager>());
    services.AddSingleton<IDockerComposeManager>(x => x.GetRequiredService<DockerComposeManager>());

    services.AddSingleton<EnvironmentVariablesManager>();
    services.AddSingleton<IConfigureEnvironmentVariables>(x => x.GetRequiredService<EnvironmentVariablesManager>());
    services.AddSingleton<IEnvironmentVariableManager>(x => x.GetRequiredService<EnvironmentVariablesManager>());

    services.AddSingleton<AppSettingsJsonManager>();
    services.AddSingleton<IConfigureAppSettingsJson>(x => x.GetRequiredService<AppSettingsJsonManager>());
    services.AddSingleton<IAppSettingsJsonManager>(x => x.GetRequiredService<AppSettingsJsonManager>());

    services.AddSingleton<IAspireManager, AspireManager>();
    services.AddSingleton<IUserSettingsManager, UserSettingsManager>();

    services.TryAddEnumerable(new[]
    {
        ServiceDescriptor.Singleton<IPipelineStep, EnsureOperatingSystemAndArchitecturePipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, EnsureAtLeastOneLeapConfigFilePipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, EnsureLeapDirectoriesCreatedPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, EnsureMkcertCertificateExistsPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, PopulateDependenciesFromYamlPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, PopulateServicesFromYamlPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, UpdateHostsFilePipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, BeforeStartingDependenciesPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, PrepareServiceRunnersPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, StartAzureCliDockerProxyPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, WireServicesAndDependenciesPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, EnsureDockerIsRunningPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, StartDockerComposePipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, AfterStartingDependenciesPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, StartAspirePipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, StartReverseProxyPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, WriteAppSettingsJsonPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, PrintEnvironmentVariablesPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, WaitForUserCancellationPipelineStep>(),
    });
});

builder.UseTelemetry();

var parser = builder.Build();
var exitCode = parser.Invoke(args);

return exitCode;
