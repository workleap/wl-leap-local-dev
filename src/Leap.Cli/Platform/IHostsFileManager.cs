namespace Leap.Cli.Platform;

internal interface IHostsFileManager
{
    Task<ISet<string>?> GetHostnamesAsync(CancellationToken cancellationToken);

    Task UpdateHostnamesAsync(IEnumerable<string> hosts, CancellationToken cancellationToken);
}