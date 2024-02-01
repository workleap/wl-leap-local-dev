using Leap.Cli.Model;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class PrintEnvironmentVariablesPipelineStep : IPipelineStep
{
    private readonly ILogger _logger;
    private readonly IEnvironmentVariableManager _environmentVariableManager;

    public PrintEnvironmentVariablesPipelineStep(
        ILogger<PrintEnvironmentVariablesPipelineStep> logger,
        IEnvironmentVariableManager environmentVariableManager)
    {
        this._logger = logger;
        this._environmentVariableManager = environmentVariableManager;
    }

    public Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var publicEnvironmentVariables = this._environmentVariableManager.EnvironmentVariables.Where(x => x.Scope == EnvironmentVariableScope.Host).ToArray();

        if (publicEnvironmentVariables.Length > 0)
        {
            this._logger.LogInformation("Environment variables:");

            foreach (var (name, value, _) in publicEnvironmentVariables)
            {
                this._logger.LogInformation("{Name}: {Value}", name, value);
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}