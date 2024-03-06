using System.Diagnostics;
using CliWrap;
using CliWrap.Buffered;
using CliWrap.Exceptions;
using Leap.Cli.Platform.Telemetry;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Platform;

internal sealed class CliWrapExecutor(ITelemetryHelper telemetryHelper, ILogger<CliWrapExecutor> logger) : ICliWrap
{
    public async Task<CommandResult> ExecuteAsync(Command command, CancellationToken forcefulCancellationToken, CancellationToken gracefulCancellationToken = default)
    {
        using var activity = this.CreateCommandActivity(command);

        this.LogCommandExecution(command);

        try
        {
            var result = await command.ExecuteAsync(forcefulCancellationToken, gracefulCancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.AddTag(TelemetryConstants.Attributes.Process.ExitCode, result.ExitCode);

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            if (ex is CommandExecutionException ceex)
            {
                activity?.AddTag(TelemetryConstants.Attributes.Process.ExitCode, ceex.ExitCode);
            }

            throw;
        }
    }

    public async Task<BufferedCommandResult> ExecuteBufferedAsync(Command command, CancellationToken cancellationToken)
    {
        using var activity = this.CreateCommandActivity(command);

        this.LogCommandExecution(command);

        try
        {
            var result = await command.ExecuteBufferedAsync(cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.AddTag(TelemetryConstants.Attributes.Process.ExitCode, result.ExitCode);

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            if (ex is CommandExecutionException ceex)
            {
                activity?.AddTag(TelemetryConstants.Attributes.Process.ExitCode, ceex.ExitCode);
            }

            throw;
        }
    }

    private Activity? CreateCommandActivity(Command command)
    {
        var activity = telemetryHelper.StartChildActivity(TelemetryConstants.ActivityNames.Process, ActivityKind.Client);

        if (activity != null)
        {
            activity.DisplayName = command.TargetFilePath;
        }

        return activity;
    }

    private void LogCommandExecution(ICommandConfiguration command)
    {
        logger.LogTrace("Executing command {Command} {Arguments}", command.TargetFilePath, command.Arguments);
    }
}