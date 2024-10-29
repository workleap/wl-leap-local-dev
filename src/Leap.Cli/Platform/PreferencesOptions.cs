using System.CommandLine;

namespace Leap.Cli.Platform;

public static class PreferencesOptions
{
    private static readonly string[] ServiceAliases = ["-s", "--service"];
    private static readonly string[] RunnerAliases = ["-r", "--runner"];

    public static Option<string> CreateServiceOption()
    {
        var option = new Option<string>(ServiceAliases)
        {
            AllowMultipleArgumentsPerToken = false,
            Description = "Service name to set preferences for.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = true
        };

        option.AddValidator(result =>
        {
            var serviceName = result.GetValueOrDefault<string>();
            if (!ServiceNameValidator.IsValid(serviceName!, out var errorMessage))
            {
                result.ErrorMessage = errorMessage;
            }
        });

        return option;
    }

    public static Option<string> CreateRunnerOption()
    {
        var option = new Option<string>(RunnerAliases)
        {
            AllowMultipleArgumentsPerToken = false,
            Description = $"Runner preference to set. Allowed values are: {string.Join(", ", Constants.AllowedRunners)}.",
            Arity = ArgumentArity.ExactlyOne,
            IsRequired = true
        };

        option.AddValidator(result =>
        {
            var runnerType = result.GetValueOrDefault<string>();
            if (!Constants.AllowedRunners.Contains(runnerType, StringComparer.OrdinalIgnoreCase))
            {
                result.ErrorMessage = $"Invalid runner type '{runnerType}'. Allowed values are: {string.Join(", ", Constants.AllowedRunners)}.";
            }
        });

        return option;
    }
}