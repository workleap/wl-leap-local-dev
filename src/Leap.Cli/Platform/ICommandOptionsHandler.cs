namespace Leap.Cli.Platform;

internal interface ICommandOptionsHandler<in TOptions>
{
    Task<int> HandleAsync(TOptions options, CancellationToken cancellationToken);
}