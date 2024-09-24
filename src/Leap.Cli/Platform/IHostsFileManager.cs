namespace Leap.Cli.Platform;

internal interface IHostsFileManager
{
    Task<ISet<string>?> GetLeapManagedHostnamesAsync(CancellationToken cancellationToken);

    Task UpdateLeapManagedHostnamesAsync(IEnumerable<string> hosts, CancellationToken cancellationToken);

    Task<ISet<string>?> GetAllCustomHostnamesAsync(CancellationToken cancellationToken);
}