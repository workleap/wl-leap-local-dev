using Leap.Cli.Configuration.Yaml;

namespace Leap.Cli.Configuration;

internal sealed class LeapYamlFile
{
    public LeapYamlFile(LeapYaml content, string path)
    {
        this.Content = content;
        this.Path = path;
    }

    public LeapYaml Content { get; set; }

    public string Path { get; }
}