namespace Leap.Cli.DockerCompose.Yaml;

internal static class DockerComposeConstants
{
    public const string Version3 = "3";

    public static class Volume
    {
        public const string ReadOnly = "ro";
        public const string ReadWrite = "rw";
    }

    public static class Restart
    {
        public const string No = "no";
        public const string Always = "always";
        public const string OnFailure = "on-failure";
        public const string UnlessStopped = "unless-stopped";
    }

    public static class Driver
    {
        public const string Bridge = "bridge";
        public const string Local = "local";
    }
}