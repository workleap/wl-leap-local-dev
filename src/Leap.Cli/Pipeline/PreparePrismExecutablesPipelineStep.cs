using Leap.Cli.Model;
using Leap.Cli.ProcessCompose;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Pipeline;

internal sealed class PreparePrismExecutablesPipelineStep : IPipelineStep
{
    private readonly IPrismManager _prismManager;
    private readonly ILogger<PreparePrismExecutablesPipelineStep> _logger;

    public PreparePrismExecutablesPipelineStep(IPrismManager prismManager, ILogger<PreparePrismExecutablesPipelineStep> logger)
    {
        this._prismManager = prismManager;
        this._logger = logger;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        // TODO only download Prism if there are services that need it
        this._logger.LogDebug("Ensure Prism binary exists...");
        await this._prismManager.EnsurePrismExecutableExistsAsync(cancellationToken);

        // TODO for each OpenAPI specification, use process compose to start a Prism instance with a dedicated port
        // later on, for one service: route all prism instances to the same port defined on the service?
        // in any case, find a way to make routing work with multiple specs for a single service
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}