using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Leap.Cli.Platform.Telemetry;

internal static class TelemetryMiddleware
{
    public static CommandLineBuilder UseTelemetry(this CommandLineBuilder builder)
    {
        return builder.AddMiddleware(TelemetryMiddlewareImplementation, MiddlewareOrder.Configuration);
    }

    private static async Task TelemetryMiddlewareImplementation(InvocationContext context, Func<InvocationContext, Task> next)
    {
        var telemetryHelper = context.BindingContext.GetRequiredService<ITelemetryHelper>();

        using var rootActivity = telemetryHelper.StartRootActivity();

        if (rootActivity != null)
        {
            rootActivity.DisplayName = context.ParseResult.GetFullCommandName();
        }

        try
        {
            await next(context);

            rootActivity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException) when (context.GetCancellationToken().IsCancellationRequested)
        {
            rootActivity?.SetStatus(ActivityStatusCode.Ok);

            throw;
        }
        catch (Exception ex)
        {
            rootActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            rootActivity?.RecordException(ex);

            throw;
        }
    }
}