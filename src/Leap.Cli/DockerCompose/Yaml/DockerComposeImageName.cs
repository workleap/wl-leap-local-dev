namespace Leap.Cli.DockerCompose.Yaml;

/// <summary>
/// This class represents the name and tag of a Docker image (like "mcr.microsoft.com/azure-storage/azurite:3.33.0"),
/// and its only purpose is to be detected by our Renovate configuration for automatic updates.
/// </summary>
internal sealed class DockerComposeImageName(string value)
{
    public static readonly DockerComposeImageName Empty = new DockerComposeImageName(string.Empty);

    public string Value { get; } = value;

    public override string ToString()
    {
        return this.Value;
    }
}