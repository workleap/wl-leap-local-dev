using System.Runtime.ExceptionServices;
using Leap.Cli.Model;
using Leap.Cli.Platform;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leap.Cli.Pipeline;

internal sealed class LeapPipeline
{
    private readonly IPipelineStep[] _steps;
    private readonly ILogger _logger;

    public LeapPipeline(IEnumerable<IPipelineStep> steps, ITelemetryHelper telemetryHelper, ILogger<LeapPipeline> logger, IOptions<LeapGlobalOptions> options)
    {
        this._steps = steps.ToArray();
        this._logger = logger;

        if (options.Value.EnableDiagnostic)
        {
            for (var i = 0; i < this._steps.Length; i++)
            {
                this._steps[i] = new InstrumentedPipelineStep(this._steps[i], telemetryHelper);
            }
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var state = new ApplicationState();

        try
        {
            await this.StartAsync(state, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            this._logger.LogInformation("Leap is shutting down...");
        }
        finally
        {
            var gracefulShutdownDelay = TimeSpan.FromSeconds(30);
            using var cts = new CancellationTokenSource(gracefulShutdownDelay);
            await this.StopAsync(state, cts.Token);
        }
    }

    private async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        foreach (var step in this._steps)
        {
            try
            {
                await step.StartAsync(state, cancellationToken);
            }
            catch (LeapException ex)
            {
                this._logger.LogError("{Error}", ex.Message);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"An unhandled exception occurred during the pipeline step '{step.GetType().Name}'.", ex);
            }
        }
    }

    private async Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var exceptions = new List<Exception>();

        foreach (var step in this._steps.Reverse())
        {
            try
            {
                await step.StopAsync(state, cancellationToken);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        if (exceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
        }
        else if (exceptions.Count > 0)
        {
            throw new AggregateException(exceptions);
        }
    }
}