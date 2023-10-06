using Leap.Cli.DockerCompose.Yaml;

namespace Leap.Cli.DockerCompose;

internal interface IDockerComposeSerializer
{
    DockerComposeYaml Deserialize(Stream stream);
    
    void Serialize(Stream stream, DockerComposeYaml dockerComposeYaml);
}