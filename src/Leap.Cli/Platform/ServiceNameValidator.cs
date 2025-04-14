namespace Leap.Cli.Platform;

/// <summary>
/// Given that we are using the same name validation logic as aspire, we copied over the validator class from aspire.
/// https://github.com/dotnet/aspire/blob/2c1822cdfbec623cbdefbc711ece00f4eddb7490/src/Aspire.Hosting/ApplicationModel/ModelName.cs#L9
/// </summary>
internal static class ServiceNameValidator
{
    /// <summary>
    /// Validate that a model name is valid.
    /// - Must start with an ASCII letter.
    /// - Must contain only ASCII letters, digits, and hyphens.
    /// - Must not end with a hyphen.
    /// - Must not contain consecutive hyphens.
    /// - Must be between 1 and 64 characters long.
    /// </summary>
    public static bool IsValid(string name, out string? validationMessage)
    {
        validationMessage = null;

        if (name.Length < 1 || name.Length > 64)
        {
            validationMessage = $"Name '{name}' is invalid. Name must be between 1 and 64 characters long.";
            return false;
        }

        var lastCharacterHyphen = false;
        for (var i = 0; i < name.Length; i++)
        {
            if (name[i] == '-')
            {
                if (lastCharacterHyphen)
                {
                    validationMessage = $"Name '{name}' is invalid. Name cannot contain consecutive hyphens.";
                    return false;
                }

                lastCharacterHyphen = true;
            }
            else if (!char.IsAsciiLetterOrDigit(name[i]))
            {
                validationMessage = $"Name '{name}' is invalid. Name must contain only ASCII letters, digits, and hyphens.";
                return false;
            }
            else
            {
                lastCharacterHyphen = false;
            }
        }

        if (!char.IsAsciiLetter(name[0]))
        {
            validationMessage = $"Name '{name}' is invalid. Name must start with an ASCII letter.";
            return false;
        }

        if (name[^1] == '-')
        {
            validationMessage = $"Name '{name}' is invalid. Name cannot end with a hyphen.";
            return false;
        }

        return true;
    }
}