using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Leap.Cli.Platform.Telemetry;

public static class TelemetryMeters
{
    public static readonly Meter LeapMeter = new Meter(TelemetryConstants.AssemblyName, TelemetryConstants.AssemblyVersion);
    private static readonly TagList Tags = new TagList { { "user_id", TelemetryAnonymizer.ComputeAnonymizedEndUserId() } };
    private static readonly Counter<long> LeapRunCounter = LeapMeter.CreateCounter<long>("leap.runs");
    private static readonly Counter<long> MongodbStartCounter = LeapMeter.CreateCounter<long>("dependency.mongodb.starts");
    private static readonly Counter<long> EventGridStartCounter = LeapMeter.CreateCounter<long>("dependency.eventgrid.starts");
    private static readonly Counter<long> RedisStartCounter = LeapMeter.CreateCounter<long>("dependency.redis.starts");
    private static readonly Counter<long> SqlServerStartCounter = LeapMeter.CreateCounter<long>("dependency.sqlserver.starts");
    private static readonly Counter<long> PostgresStartCounter = LeapMeter.CreateCounter<long>("dependency.postgres.starts");
    private static readonly Counter<long> AzuriteStartCounter = LeapMeter.CreateCounter<long>("dependency.azurite.starts");

    public static void TrackLeapRun() => LeapRunCounter.Add(1, Tags);
    public static void TrackMongodbStart() => MongodbStartCounter.Add(1, Tags);
    public static void TrackEventGridStart() => EventGridStartCounter.Add(1, Tags);
    public static void TrackRedisStart() => RedisStartCounter.Add(1, Tags);
    public static void TrackSqlServerStart() => SqlServerStartCounter.Add(1, Tags);
    public static void TrackPostgresStart() => PostgresStartCounter.Add(1, Tags);
    public static void TrackAzuriteStart() => AzuriteStartCounter.Add(1, Tags);
}