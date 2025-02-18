using Meziantou.Framework;

namespace Workleap.Leap.Testing;

internal static class PathHelpers
{
    public static FullPath GetGitRepositoryRoot()
    {
        var path = FullPath.CurrentDirectory();
        if (path.TryFindFirstAncestorOrSelf(path => Directory.Exists(path / ".git"), out var result))
        {
            return result;
        }

        throw new InvalidOperationException($"Cannot find git repository root from '{path}'");
    }
}