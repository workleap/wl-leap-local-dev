using Leap.Cli.ProcessCompose.Yaml;

namespace Leap.Cli.ProcessCompose;

internal interface IConfigureProcessCompose
{
    ProcessComposeYaml Configuration { get; }
}