using Microsoft.Extensions.Logging;

namespace Leap.Cli.Extensions;

internal static partial class LoggingExtensions
{
    [LoggerMessage(1, LogLevel.Debug, "Pipeline step '{StepName}' is skipped because the feature flag '{FlagName}' is not enabled")]
    public static partial void LogPipelineStepSkipped(this ILogger logger, string stepName, string flagName);
}
