namespace Workleap.Leap.Testing;

// https://github.com/dotnet/aspire/blob/b27731dd0abde6a3b1acdb4ef3ee7e39f6a9188a/src/Aspire.Dashboard/Model/KnownResourceState.cs#L19
internal enum KnownResourceState
{
    Finished,
    Exited,
    FailedToStart,
    Starting,
    Running,
    Building,
    Hidden,
    Waiting,
    Stopping,
    Unknown,
    RuntimeUnhealthy,
    NotStarted
}