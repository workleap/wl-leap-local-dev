using System.CommandLine;

namespace Leap.Cli.Platform;

internal sealed class LeapGlobalOptions
{
    public static readonly Option<bool> IsQuietOption = new Option<bool>(["--quiet"])
    {
        Description = "Hide debugging messages.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<string[]> FeatureFlagsOption = new Option<string[]>(["--feature-flags"])
    {
        AllowMultipleArgumentsPerToken = true,
        Description = "Provide one or more feature flags to enable specific features.",
        Arity = ArgumentArity.ZeroOrMore,
        IsHidden = true,
    };

    public static readonly Option<bool> EnableDiagnosticOption = new Option<bool>(["--diagnostic"])
    {
        Description = "Enable diagnostic mode.",
        Arity = ArgumentArity.ZeroOrOne,
        IsHidden = true
    };

    public const string SkipVersionCheckOptionName = "--skip-version-check";

    public static readonly Option<bool> SkipVersionCheckOption = new Option<bool>([SkipVersionCheckOptionName])
    {
        Description = "Don't check if a newer version of Leap is available.",
        Arity = ArgumentArity.ZeroOrOne,
    };

    public bool IsQuiet { get; set; }

    public string[] FeatureFlags { get; set; } = [];

    public bool EnableDiagnostic { get; set; }

    public bool SkipVersionCheck { get; set; }
}