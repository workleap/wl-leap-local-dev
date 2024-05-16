using System.CommandLine;
using System.CommandLine.Parsing;
using Leap.Cli.Platform.Logging;

namespace Leap.Cli.Platform;

internal sealed class LeapGlobalOptions
{
    // https://learn.microsoft.com/en-us/dotnet/standard/commandline/model-binding#custom-validation-and-binding
    public static readonly Option<LoggerVerbosity> VerbosityOption = new(["--verbosity"], parseArgument: ParseVerbosityArgument, isDefault: true)
    {
        Description = "Change the verbosity level. Allowed values are quiet, normal, and diagnostic.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    private static LoggerVerbosity ParseVerbosityArgument(ArgumentResult result)
    {
        const LoggerVerbosity defaultVerbosity = LoggerVerbosity.Normal;

        if (result.Tokens.Count == 0)
        {
            return defaultVerbosity;
        }

        if (Enum.TryParse<LoggerVerbosity>(result.Tokens[0].Value, ignoreCase: true, out var verbosity))
        {
            return verbosity;
        }

        result.ErrorMessage = $"Invalid value '{result.Tokens[0].Value}' for --verbosity. Allowed values are quiet, normal, and diagnostic.";
        return defaultVerbosity;
    }

    public static readonly Option<string[]> FeatureFlagsOption = new(["--feature-flags"])
    {
        AllowMultipleArgumentsPerToken = true,
        Description = "Provide one or more feature flags to enable specific features.",
        Arity = ArgumentArity.ZeroOrMore,
        IsHidden = true,
    };

    public static readonly Option<bool> EnableDiagnosticOption = new(["--diagnostic"])
    {
        Description = "Enable diagnostic mode.",
        Arity = ArgumentArity.ZeroOrOne,
        IsHidden = true
    };

    public const string SkipVersionCheckOptionName = "--skip-version-check";

    public static readonly Option<bool> SkipVersionCheckOption = new([SkipVersionCheckOptionName])
    {
        Description = "Don't check if a newer version of Leap is available.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public LoggerVerbosity Verbosity { get; set; }

    public string[] FeatureFlags { get; set; } = [];

    public bool EnableDiagnostic { get; set; }

    public bool SkipVersionCheck { get; set; }
}