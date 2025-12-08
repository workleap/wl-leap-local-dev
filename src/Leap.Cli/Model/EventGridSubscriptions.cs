namespace Leap.Cli.Model;

internal sealed class EventGridSubscriptions(IEnumerable<string> subscriptions)
    : HashSet<string>(subscriptions, StringComparer.OrdinalIgnoreCase);