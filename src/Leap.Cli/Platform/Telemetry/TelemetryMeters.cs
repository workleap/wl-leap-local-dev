using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Leap.Cli.Platform.Telemetry;

public static class TelemetryMeters
{
    public static readonly Meter LeapMeter = new(TelemetryConstants.AssemblyName, TelemetryConstants.AssemblyVersion);

    private static readonly TagList Tags = new()
    {
        { "user_id", TelemetryAnonymizer.ComputeAnonymizedEndUserId() }
    };

    private static readonly Counter<long> LeapRunCounter = LeapMeter.CreateCounter<long>("leap.runs");
    private static readonly Counter<long> LeapPreferencesCounter = LeapMeter.CreateCounter<long>("leap.preferences");
    private static readonly Counter<long> MongodbStartCounter = LeapMeter.CreateCounter<long>("dependency.mongodb.starts");
    private static readonly Counter<long> EventGridStartCounter = LeapMeter.CreateCounter<long>("dependency.eventgrid.starts");
    private static readonly Counter<long> RedisStartCounter = LeapMeter.CreateCounter<long>("dependency.redis.starts");
    private static readonly Counter<long> SqlServerStartCounter = LeapMeter.CreateCounter<long>("dependency.sqlserver.starts");
    private static readonly Counter<long> PostgresStartCounter = LeapMeter.CreateCounter<long>("dependency.postgres.starts");
    private static readonly Counter<long> AzuriteStartCounter = LeapMeter.CreateCounter<long>("dependency.azurite.starts");
    private static readonly Counter<long> FusionAuthStartCounter = LeapMeter.CreateCounter<long>("dependency.fusionauth.starts");
    private static readonly Counter<long> LeapServicesStartCounter = LeapMeter.CreateCounter<long>("leap.services.count");
    private static readonly Counter<long> LeapPreferencesCommandCounter = LeapMeter.CreateCounter<long>("leap.preferences.command");
    private static readonly Counter<long> WaitForDebuggerCounter = LeapMeter.CreateCounter<long>("leap.waitfordebugger.command");
    private static readonly Counter<long> DockerResourceCommandCounter = LeapMeter.CreateCounter<long>("leap.docker.resource.command");
    private static readonly Counter<long> LeapStartDurationCounter = LeapMeter.CreateCounter<long>("leap.starts.duration_ms");
    private static readonly Counter<long> LeapRunDurationCounter = LeapMeter.CreateCounter<long>("leap.runs.duration_ms");
    private static readonly Counter<long> OutdatedLeapCounter = LeapMeter.CreateCounter<long>("leap.outdated");

    public static void TrackLeapRun() => LeapRunCounter.Add(1, Tags);
    public static void TrackPreferencesRun() => LeapPreferencesCounter.Add(1, Tags);
    public static void TrackMongodbStart() => MongodbStartCounter.Add(1, Tags);
    public static void TrackEventGridStart() => EventGridStartCounter.Add(1, Tags);
    public static void TrackRedisStart() => RedisStartCounter.Add(1, Tags);
    public static void TrackSqlServerStart() => SqlServerStartCounter.Add(1, Tags);
    public static void TrackPostgresStart() => PostgresStartCounter.Add(1, Tags);
    public static void TrackAzuriteStart() => AzuriteStartCounter.Add(1, Tags);
    public static void TrackFusionAuthStart() => FusionAuthStartCounter.Add(1, Tags);

    public static void TrackNumberServices(int servicesCount, Dictionary<string, int> runnerCounts)
    {
        var tags = GetTagsWithDefault();

        foreach (var (key, value) in runnerCounts)
        {
            tags.Add(key, value);
        }

        LeapServicesStartCounter.Add(servicesCount, tags);
    }

    public static void TrackPreferencesCommand() => LeapPreferencesCommandCounter.Add(1, Tags);

    public static void TrackWaitForDebuggerCommand(string serviceName)
    {
        var tags = GetTagsWithDefault();
        tags.Add("service", serviceName);
        WaitForDebuggerCounter.Add(1, tags);
    }

    public static void TrackDockerResourceCommand(string serviceName)
    {
        var tags = GetTagsWithDefault();
        tags.Add("service", serviceName);
        DockerResourceCommandCounter.Add(1, tags);
    }

    public static void TrackLeapStartDuration(TimeSpan duration) => LeapStartDurationCounter.Add(Convert.ToInt64(duration.TotalMilliseconds), Tags);
    public static void TrackLeapRunDuration(TimeSpan duration) => LeapRunDurationCounter.Add(Convert.ToInt64(duration.TotalMilliseconds), Tags);
    public static void TrackOutdatedLeap() => OutdatedLeapCounter.Add(1, Tags);

    private static TagList GetTagsWithDefault()
    {
        var tags = new TagList();
        foreach (var (key, value) in Tags)
        {
            tags.Add(key, value);
        }

        return tags;
    }
}