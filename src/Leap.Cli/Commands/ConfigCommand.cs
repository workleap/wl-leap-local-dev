using System.CommandLine;

namespace Leap.Cli.Commands;

internal sealed class ConfigCommand()
    : Command("config", "Manage registered Leap YAML files");