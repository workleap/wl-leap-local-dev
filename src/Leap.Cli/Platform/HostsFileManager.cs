using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Leap.Cli.Platform;

// TODO unit test this class by faking the file system and platform
internal sealed class HostsFileManager : IHostsFileManager
{
    private const string StartDelimiter = "# Begin lines managed by Leap CLI";
    private const string EndDelimiter = "# End lines managed by Leap CLI";

    // Leap hosts file line entry regex
    private static readonly Regex HostsFileLineRegex = new Regex(@"^127.0.0.1\s+(?<hostname>[a-z0-9\-\.]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> WellKnownNonCustomHostnames = new(StringComparer.OrdinalIgnoreCase)
    {
        "host.docker.internal", "gateway.docker.internal", "kubernetes.docker.internal", "localhost",
    };

    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly string _hostsFilePath;

    public HostsFileManager(IFileSystem fileSystem, IPlatformHelper platformHelper, ILogger<HostsFileManager> logger)
    {
        this._fileSystem = fileSystem;
        this._logger = logger;
        this._hostsFilePath = platformHelper.CurrentOS == OSPlatform.Windows
            ? @"C:\Windows\System32\drivers\etc\hosts"
            : "/etc/hosts";
    }

    public async Task<ISet<string>?> GetAllCustomHostnamesAsync(CancellationToken cancellationToken)
    {
        var lines = await this.GetHostsFileLines(cancellationToken);
        if (lines == null)
        {
            return null;
        }

        var hostnames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (HostsFileLineRegex.Match(line) is { Success: true } match)
            {
                if (!WellKnownNonCustomHostnames.Contains(match.Groups["hostname"].Value))
                {
                    hostnames.Add(match.Groups["hostname"].Value);
                }
            }
        }

        return hostnames;
    }

    public async Task<ISet<string>?> GetLeapManagedHostnamesAsync(CancellationToken cancellationToken)
    {
        var lines = await this.GetHostsFileLines(cancellationToken);
        if (lines == null)
        {
            return null;
        }

        var startDelimiterIndex = Array.LastIndexOf(lines, StartDelimiter);
        var endDelimiterIndex = Array.LastIndexOf(lines, EndDelimiter);

        var delimitersNotFound = startDelimiterIndex == -1 || endDelimiterIndex == -1;
        if (delimitersNotFound)
        {
            return new HashSet<string>(capacity: 0);
        }

        var misplacedDelimiters = startDelimiterIndex > endDelimiterIndex;
        if (misplacedDelimiters)
        {
            this._logger.LogWarning("Hosts file '{HostsFilePath}' is malformed. The start delimiter is after the end delimiter.", this._hostsFilePath);
            return null;
        }

        var leapLines = lines.Skip(startDelimiterIndex + 1).Take(endDelimiterIndex - startDelimiterIndex - 1);
        var uniqueHostnames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in leapLines)
        {
            if (HostsFileLineRegex.Match(line) is { Success: true } match)
            {
                uniqueHostnames.Add(match.Groups["hostname"].Value);
            }
        }

        return uniqueHostnames;
    }

    public async Task UpdateLeapManagedHostnamesAsync(IEnumerable<string> hosts, CancellationToken cancellationToken)
    {
        var lines = await this.GetHostsFileLines(cancellationToken);
        if (lines == null)
        {
            return;
        }

        var startDelimiterIndex = Array.LastIndexOf(lines, StartDelimiter);
        var endDelimiterIndex = Array.LastIndexOf(lines, EndDelimiter);

        List<string> beforeLeapLines; // Lines before the start delimiter (not including the delimiter)
        List<string> leapLines; // Lines between the start and end delimiters (not including the delimiters)
        List<string> afterLeapLines; // Lines after the end delimiter (not including the delimiter)

        var delimitersNotFound = startDelimiterIndex == -1 || endDelimiterIndex == -1;
        var misplacedDelimiters = startDelimiterIndex > endDelimiterIndex;

        if (delimitersNotFound || misplacedDelimiters)
        {
            beforeLeapLines = lines.ToList();
            leapLines = new List<string>();
            afterLeapLines = new List<string>();
        }
        else
        {
            beforeLeapLines = lines.Take(startDelimiterIndex).ToList();
            leapLines = lines.Skip(startDelimiterIndex + 1).Take(endDelimiterIndex - startDelimiterIndex - 1).ToList();
            afterLeapLines = lines.Skip(endDelimiterIndex + 1).ToList();
        }

        var uniqueHosts = new HashSet<string>(hosts, StringComparer.OrdinalIgnoreCase);

        leapLines.Clear();
        leapLines.Add(StartDelimiter);
        leapLines.AddRange(uniqueHosts.Select(x => $"127.0.0.1 {x.ToLowerInvariant()}"));
        leapLines.Add(EndDelimiter);

        var newLines = beforeLeapLines.Concat(leapLines).Concat(afterLeapLines);

        // We don't want to leave the hosts file in a bad state if the process is shutting down while we're writing to it
        try
        {
            await this._fileSystem.File.WriteAllLinesAsync(this._hostsFilePath, newLines, CancellationToken.None);
        }
        catch (IOException ex)
        {
            this._logger.LogWarning("Hosts file '{HostsFilePath}' could not be written: {Reason}", this._hostsFilePath, ex.Message);
        }
    }

    private async Task<string[]?> GetHostsFileLines(CancellationToken cancellationToken)
    {
        try
        {
            return await this._fileSystem.File.ReadAllLinesAsync(this._hostsFilePath, cancellationToken);
        }
        catch (IOException ex)
        {
            this._logger.LogWarning("Hosts file '{HostsFilePath}' could not be read: {Reason}", this._hostsFilePath, ex.Message);
            return null;
        }
    }
}