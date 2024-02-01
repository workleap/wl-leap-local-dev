using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Platform;

internal sealed class CliWrapExecutor : ICliWrap
{
    private readonly ILogger _cliWrap;

    public CliWrapExecutor(ILogger<CliWrapExecutor> cliWrap)
    {
        this._cliWrap = cliWrap;
    }

    public Task<CommandResult> ExecuteAsync(Command command, CancellationToken forcefulCancellationToken, CancellationToken gracefulCancellationToken = default)
    {
        this.LogCommandExecution(command);
        return command.ExecuteAsync(forcefulCancellationToken, gracefulCancellationToken);
    }

    public Task<BufferedCommandResult> ExecuteBufferedAsync(Command command, CancellationToken cancellationToken)
    {
        this.LogCommandExecution(command);
        return command.ExecuteBufferedAsync(cancellationToken);
    }

    private void LogCommandExecution(ICommandConfiguration command)
    {
        this._cliWrap.LogTrace("Executing command {Command} {Arguments}", command.TargetFilePath, command.Arguments);
    }
}