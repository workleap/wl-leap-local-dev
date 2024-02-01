namespace Leap.Cli.ProcessCompose;

internal interface IPrismManager
{
    string PrismExecutablePath { get; }

    Task EnsurePrismExecutableExistsAsync(CancellationToken cancellationToken);
}