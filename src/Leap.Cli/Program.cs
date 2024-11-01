using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Net.Http.Headers;
using System.Net.Mime;
using Leap.Cli;
using Leap.Cli.Aspire;
using Leap.Cli.Commands;
using Leap.Cli.Configuration;
using Leap.Cli.Dependencies;
using Leap.Cli.Dependencies.Azurite;
using Leap.Cli.DockerCompose;
using Leap.Cli.Model;
using Leap.Cli.Pipeline;
using Leap.Cli.Platform;
using Leap.Cli.Platform.Logging;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ConsoleExtensions = Leap.Cli.Platform.ConsoleExtensions;

StartupDefaults.SetEnvironmentVariables();
StartupDefaults.SetInvariantCulture();
StartupDefaults.SetUtf8Encoding();

var rootCommand = new RootCommand("Workleap's Local Environment Application Proxy")
{
    new RunCommand(),
    new Command("preferences", "Configure preferences for Leap services")
    {
        new PreferencesSetCommand(),
        new PreferencesListCommand(),
        new PreferencesRemoveCommand(),
    },
    new UpdateHostsFileCommand(),
    new AddCertificateAuthorityToComputerRootStoreCommand(),
};

rootCommand.AddGlobalOption(LeapGlobalOptions.VerbosityOption);
rootCommand.AddGlobalOption(LeapGlobalOptions.FeatureFlagsOption);
rootCommand.AddGlobalOption(LeapGlobalOptions.EnableDiagnosticOption);
rootCommand.AddGlobalOption(LeapGlobalOptions.SkipVersionCheckOption);
rootCommand.AddGlobalOption(LeapGlobalOptions.ProfilesOption);

rootCommand.Name = "leap";

var builder = new CommandLineBuilder(rootCommand);

builder.UseDefaults();
builder.UseDependencyInjection((services, context) =>
{
    services.Configure<LeapGlobalOptions>(options =>
    {
        options.Verbosity = context.ParseResult.GetValueForOption(LeapGlobalOptions.VerbosityOption);
        options.FeatureFlags = context.ParseResult.GetValueForOption(LeapGlobalOptions.FeatureFlagsOption) ?? [];
        options.EnableDiagnostic = context.ParseResult.GetValueForOption(LeapGlobalOptions.EnableDiagnosticOption);
        options.SkipVersionCheck = context.ParseResult.GetValueForOption(LeapGlobalOptions.SkipVersionCheckOption);
        options.Profiles = context.ParseResult.GetValueForOption(LeapGlobalOptions.ProfilesOption) ?? [];
    });

    services.AddLogging(x => x.AddColoredConsoleLogger(LoggingSource.Leap));

    services.AddSingleton<IFeatureManager, FeatureManager>();
    services.AddSingleton<IFileSystem, FileSystem>();
    services.AddSingleton<ICliWrap, CliWrapExecutor>();
    services.AddSingleton<IPlatformHelper, PlatformHelper>();
    services.AddSingleton<IPortManager, PortManager>();
    services.AddSingleton<IHostsFileManager, HostsFileManager>();
    services.AddSingleton<ITelemetryHelper, TelemetryHelper>();
    services.AddSingleton<AzuriteAuthenticationHandler>();

    services.AddHttpClient(AzuriteConstants.HttpClientName)
        .AddHttpMessageHandler<AzuriteAuthenticationHandler>();

    services.AddSingleton<LeapConfigManager>();
    services.AddSingleton<PreferencesSettingsManager>();
    services.AddSingleton<ILeapYamlAccessor>(x => x.GetRequiredService<LeapConfigManager>());

    services.AddSingleton<IDependencyHandler, MongoDependencyHandler>();
    services.AddSingleton<IDependencyHandler, RedisDependencyHandler>();
    services.AddSingleton<IDependencyHandler, AzuriteDependencyHandler>();
    services.AddSingleton<IDependencyHandler, SqlServerDependencyHandler>();
    services.AddSingleton<IDependencyHandler, PostgresDependencyHandler>();
    services.AddSingleton<IDependencyHandler, EventGridDependencyHandler>();

    services.AddSingleton<IDependencyYamlHandler<MongoDependencyYaml>, MongoDependencyYamlHandler>();
    services.AddSingleton<IDependencyYamlHandler<RedisDependencyYaml>, RedisDependencyYamlHandler>();
    services.AddSingleton<IDependencyYamlHandler<AzuriteDependencyYaml>, AzuriteDependencyYamlHandler>();
    services.AddSingleton<IDependencyYamlHandler<SqlServerDependencyYaml>, SqlServerDependencyYamlHandler>();
    services.AddSingleton<IDependencyYamlHandler<PostgresDependencyYaml>, PostgresDependencyYamlHandler>();
    services.AddSingleton<IDependencyYamlHandler<EventGridDependencyYaml>, EventGridDependencyYamlHandler>();

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
    services.AddSingleton<INuGetPackageDownloader, NuGetPackageDownloader>();
    services.AddSingleton<MkcertCertificateManager>();

    services.AddSingleton<AzureDevOpsAuthenticator>();
    services.AddTransient<AzureDevOpsAuthenticationHandler>();
    services.AddHttpClient(Constants.AzureDevOps.HttpClientName)
        .AddHttpMessageHandler<AzureDevOpsAuthenticationHandler>()
        .ConfigureHttpClient(x =>
        {
            // See https://github.com/microsoft/azure-devops-auth-samples/blob/master/ManagedClientConsoleAppSample/Program.cs
            x.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
            x.DefaultRequestHeaders.Add("User-Agent", "Leap");
            x.DefaultRequestHeaders.Add("X-TFS-FedAuthRedirect", "Suppress");
            x.Timeout = Timeout.InfiniteTimeSpan; // Cancellation is driven by cancellation tokens provided to the HttpClient
        });

    services.TryAddEnumerable(new[]
    {
        ServiceDescriptor.Singleton<IPipelineStep, TrackLeapRunDurationPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, CheckForUpdatesPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, EnsureOperatingSystemAndArchitecturePipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, EnsureAtLeastOneLeapConfigFilePipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, EnsureLeapDirectoriesCreatedPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, BeginAspireDownloadTaskPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, EnsureMkcertCertificateExistsPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, CreateCertificateAuthorityBundlePipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, PopulateDependenciesFromYamlPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, PopulateServicesFromYamlPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, UpdateHostsFilePipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, SetupDependencyHandlersPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, PrepareServiceRunnersPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, StartAzureCliDockerProxyPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, WireServicesAndDependenciesPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, EnsureDockerIsRunningPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, WriteDockerComposeFilePipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, PrepareReverseProxyPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, WriteAppSettingsJsonPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, PrintEnvironmentVariablesPipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, StartAspirePipelineStep>(),
        ServiceDescriptor.Singleton<IPipelineStep, WaitForUserCancellationPipelineStep>(),
    });

    services.AddSingleton<LeapPipeline>();
});

builder.UseTelemetry();

builder.UseExceptionHandler(PrintDemystifiedException);

var parser = builder.Build();
var exitCode = parser.Invoke(args);

return exitCode;

// Reference: https://github.com/dotnet/command-line-api/blob/2.0.0-beta4.22272.1/src/System.CommandLine/Builder/CommandLineBuilderExtensions.cs#L307
static void PrintDemystifiedException(Exception exception, InvocationContext context)
{
    if (exception is not OperationCanceledException)
    {
        ConsoleExtensions.SetTerminalForeground(ConsoleColor.Red);

        Console.Write(context.LocalizationResources.ExceptionHandlerHeader());
        Console.WriteLine(exception.Demystify());

        ConsoleExtensions.ResetTerminalForegroundColor();
    }

    context.ExitCode = 1;
}
