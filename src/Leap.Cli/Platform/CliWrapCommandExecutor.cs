using CliWrap;
using CliWrap.Buffered;

namespace Leap.Cli.Platform;

internal sealed class CliWrapCommandExecutor : ICliWrapCommandExecutor
{
    public Task<CommandResult> ExecuteAsync(Command command, CancellationToken forcefulCancellationToken, CancellationToken gracefulCancellationToken = default)
    {
        return command.ExecuteAsync(forcefulCancellationToken, gracefulCancellationToken);
    }

    public Task<BufferedCommandResult> ExecuteBufferedAsync(Command command, CancellationToken cancellationToken)
    {
        return command.ExecuteBufferedAsync(cancellationToken);
    }
}