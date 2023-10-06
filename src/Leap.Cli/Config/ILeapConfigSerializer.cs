namespace Leap.Cli.Config;

public interface ILeapConfigSerializer
{
    Leap Deserialize(Stream stream);

    void Serialize(Stream stream, Leap leap);
}