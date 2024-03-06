using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Leap.Cli.Platform.Telemetry;

internal sealed class TelemetryHelper : ITelemetryHelper, IDisposable
{
    private static readonly ActivitySource LeapActivitySource = new ActivitySource(TelemetryConstants.AssemblyName, TelemetryConstants.AssemblyVersion);

    private readonly object _lock = new object();
    private readonly TracerProvider _tracerProvider;
    private readonly MeterProvider _meterProvider;

    private Activity? _rootActivity;

    public TelemetryHelper(IPlatformHelper platformHelper)
    {
        var resourceBuilder = ResourceBuilder.CreateEmpty()
            .AddTelemetrySdk()
            .AddService(TelemetryConstants.AssemblyName, serviceVersion: TelemetryConstants.AssemblyVersion);

        var tracerProviderBuilder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddProcessor(new SetEndUserIdTagProcessor())
            .AddSource(TelemetryConstants.AssemblyName);

        // TODO register metrics here or remove this code if we don't use metrics at all
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder);

        if (platformHelper.IsRunningOnBuildAgent)
        {
            tracerProviderBuilder.AddConsoleExporter();
            meterProviderBuilder.AddConsoleExporter();
        }
        else if (platformHelper is { IsRunningOnStableVersion: true, IsRunningInReleaseConfiguration: true })
        {
            // TODO use the real production connection string, this is the dev one for now
            const string prodAppInsightsConnectionString = "InstrumentationKey=dc5612a0-06c6-4796-abf3-54b48d9e990e;IngestionEndpoint=https://canadacentral-1.in.applicationinsights.azure.com/;LiveEndpoint=https://canadacentral.livediagnostics.monitor.azure.com/";
            tracerProviderBuilder.AddAzureMonitorTraceExporter(x => x.ConnectionString = prodAppInsightsConnectionString);
            meterProviderBuilder.AddAzureMonitorMetricExporter(x => x.ConnectionString = prodAppInsightsConnectionString);
        }
        else
        {
            const string devAppInsightsConnectionString = "InstrumentationKey=dc5612a0-06c6-4796-abf3-54b48d9e990e;IngestionEndpoint=https://canadacentral-1.in.applicationinsights.azure.com/;LiveEndpoint=https://canadacentral.livediagnostics.monitor.azure.com/";
            tracerProviderBuilder.AddAzureMonitorTraceExporter(x => x.ConnectionString = devAppInsightsConnectionString);
            meterProviderBuilder.AddAzureMonitorMetricExporter(x => x.ConnectionString = devAppInsightsConnectionString);
        }

        this._tracerProvider = tracerProviderBuilder.Build();
        this._meterProvider = meterProviderBuilder.Build();
    }

    public Activity? StartRootActivity()
    {
        lock (this._lock)
        {
            if (this._rootActivity != null)
            {
                // The root activity has already been started
                return this._rootActivity;
            }

            // ActivityKind.Consumer translates to a "request" telemetry type in Application Insights
            // https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-add-modify?tabs=net#add-custom-spans
            this._rootActivity = LeapActivitySource.StartActivity(TelemetryConstants.ActivityNames.Root, ActivityKind.Consumer, parentId: null);

            return this._rootActivity;
        }
    }

    public Activity? StartChildActivity(string name, ActivityKind kind)
    {
        lock (this._lock)
        {
            if (this._rootActivity == null)
            {
                // Cannot start a child activity before the root activity has been started
                return null;
            }

            return LeapActivitySource.StartActivity(name, kind);
        }
    }

    public void StopRootActivity()
    {
        lock (this._lock)
        {
            if (this._rootActivity != null)
            {
                this._rootActivity.Dispose();
                this._rootActivity = null;
            }
        }
    }

    public void Dispose()
    {
        this.StopRootActivity();
        this.ForceFlushTraces();

        this._tracerProvider.Dispose();
        this._meterProvider.Dispose();
    }

    private void ForceFlushTraces()
    {
        // Same timeout than the one used by the .NET OTel implementation when shutting down.
        // At least the Application Insights OTel exporter keeps the traces on the file system in case there's a network issue / timeout.
        // https://github.com/open-telemetry/opentelemetry-dotnet/blob/core-1.7.0/src/OpenTelemetry/Trace/TracerProviderSdk.cs#L383
        const int forceFlushTimeoutMs = 5000;

        // The .NET OTel implementation doesn't seem to flush the traces on disposal (at least for AppInsights).
        // We observed (using Fiddler) an additional trace being sent to AppInsights ("/track" endpoint) when we force flush.
        // https://github.com/open-telemetry/opentelemetry-dotnet/blob/core-1.7.0/src/OpenTelemetry/BatchExportProcessor.cs#L88-L99
        this._tracerProvider?.ForceFlush(forceFlushTimeoutMs);
    }
}

internal interface ITelemetryHelper
{
    Activity? StartRootActivity();

    Activity? StartChildActivity(string name, ActivityKind kind);

    void StopRootActivity();
}