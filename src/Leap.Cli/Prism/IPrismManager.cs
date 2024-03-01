namespace Leap.Cli.Prism;

internal interface IPrismManager
{
    string PrismExecutablePath { get; }

    Task EnsurePrismExecutableExistsAsync(CancellationToken cancellationToken);
}