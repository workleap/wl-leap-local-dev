using Leap.Cli.Configuration;
using Leap.Cli.Dependencies;
using Leap.Cli.Model;
using Spectre.Console;

namespace Leap.Cli.Pipeline;

internal sealed class PopulateDependenciesPipelineStep : IPipelineStep
{
    private readonly ILeapYamlAccessor _leapYamlAccessor;
    private readonly IEnumerable<IDependencyYamlHandler> _dependencyYamlHandlers;
    private readonly IAnsiConsole _console;

    public PopulateDependenciesPipelineStep(ILeapYamlAccessor leapYamlAccessor, IEnumerable<IDependencyYamlHandler> dependencyYamlHandlers, IAnsiConsole console)
    {
        this._leapYamlAccessor = leapYamlAccessor;
        this._dependencyYamlHandlers = dependencyYamlHandlers;
        this._console = console;
    }

    public async Task<PipelineStepResult> StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        var leapConfigs = await this._leapYamlAccessor.GetAllAsync(cancellationToken);

        if (leapConfigs.Length == 0)
        {
            this._console.MarkupLine("[yellow]You must first create a leap.yml file in the current directory using 'leap init'.[/]");
            return PipelineStepResult.Stop;
        }

        var dependenciesYaml = leapConfigs.SelectMany(x => x.Dependencies).GroupBy(x => x.Type);

        foreach (var dependencyGroup in dependenciesYaml)
        {
            var dependencyHandler = this._dependencyYamlHandlers.FirstOrDefault(handler => handler.CanHandle(dependencyGroup.Key));

            if (dependencyHandler == null)
            {
                throw new InvalidOperationException($"No yaml dependency handler found for dependency type {dependencyGroup.Key}");
            }

            var dependencyYaml = dependencyGroup.Aggregate(dependencyGroup.First(), dependencyHandler.Merge);
            var dependency = dependencyHandler.ToDependencyModel(dependencyYaml);

            state.Dependencies.Add(dependency);
        }

        return PipelineStepResult.Continue;
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}