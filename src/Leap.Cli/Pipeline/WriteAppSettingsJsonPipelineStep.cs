using Leap.Cli.Model;

namespace Leap.Cli.Pipeline;

internal sealed class WriteAppSettingsJsonPipelineStep : IPipelineStep
{
    private readonly IAppSettingsJsonManager _appSettingsJson;

    public WriteAppSettingsJsonPipelineStep(IAppSettingsJsonManager appSettingsJson)
    {
        this._appSettingsJson = appSettingsJson;
    }

    public async Task StartAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        await this._appSettingsJson.WriteUpdatedAppSettingsJson(cancellationToken);
    }

    public Task StopAsync(ApplicationState state, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}