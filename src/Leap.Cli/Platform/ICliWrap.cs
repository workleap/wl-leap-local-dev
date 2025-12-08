using CliWrap;
using CliWrap.Buffered;

namespace Leap.Cli.Platform;

internal interface ICliWrap
{
    Task<CommandResult> ExecuteAsync(Command command, CancellationToken forcefulCancellationToken, CancellationToken gracefulCancellationToken = default);

    Task<BufferedCommandResult> ExecuteBufferedAsync(Command command, CancellationToken cancellationToken);
}