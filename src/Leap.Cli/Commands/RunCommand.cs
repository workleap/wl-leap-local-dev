using Leap.Cli.Platform;
using CliWrap;
using Spectre.Console;
using Leap.Cli.DockerCompose;
using Leap.Cli.DockerCompose.Yaml;

namespace Leap.Cli.Commands;

internal sealed class RunCommand : Command<RunCommand.Options, RunCommand.OptionsHandler>
{
    public RunCommand()
        : base("run", "Run Leap")
    {
    }

    internal sealed class Options : ICommandOptions
    {
    }

    internal sealed class OptionsHandler : ICommandOptionsHandler<Options>
    {
        private const int MongoPort = 27217; 
        private const string MongoVolume = "mongodb1_data"; 
        private const string Network = "leap-network";
        private static readonly string LeapPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".leap");

        private readonly IAnsiConsole _console;

        public OptionsHandler(IAnsiConsole console)
        {
            this._console = console;
        }

        public async Task<int> HandleAsync(Options options, CancellationToken cancellationToken)
        {
            var yaml = new DockerComposeYaml
            {
                Services =
                {
                    ["mongodb1"] = new()
                    {
                        Image = "mongo:6.0",
                        Command = $"--replSet rs0 --bind_ip_all --port {MongoPort}",
                        Restart = DockerComposeConstants.Restart.UnlessStopped,
                        SecurityOption =
                        {
                            "no-new-privileges:true",
                        },
                        Ports = { new(MongoPort, MongoPort) },
                        Volumes =
                        {
                            new(MongoVolume, "/data/db", "rw"),
                        },
                        Networks = { Network },
                        ExtraHosts = new List<string>
                        {
                            "host.docker.internal:host-gateway",
                        },
                        Deploy = new()
                        {
                            Resources = new()
                            {
                                Limits = new()
                                {
                                    Cpus = "0.5",
                                    Memory = "500M",
                                },
                            },
                        },
                    },
                },
                Volumes =
                {
                    [MongoVolume] = null,
                },
                Networks =
                {
                    [Network] = new DockerComposeNetworkYaml()
                    {
                        Driver = "bridge",
                    },
                },
            };

            var serializer = new DockerComposeSerializer();
            await using (var output = File.Create(Path.Join(LeapPath, "docker-compose.yml")))
            {
                serializer.Serialize(output, yaml);
            }
                
            var result = await CliWrap.Cli.Wrap("docker")
                .WithWorkingDirectory(LeapPath)
                .WithArguments(new[]
                {
                "compose",
                "up",
                "--pull", "missing",
                "--build",
                "--remove-orphans",
                "--wait",
                })
                .WithStandardOutputPipe(PipeTarget.ToDelegate(this._console.WriteLine))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(this._console.WriteLine))
                .ExecuteAsync(cancellationToken);

            var exitCode = 0;
            var nbRetry = 5;
            await Task.Delay(1000, cancellationToken);
            do
            {
                this._console.WriteLine("Starting replicate...");
                try
                {
                    result = await CliWrap.Cli.Wrap("docker")
                        .WithWorkingDirectory(LeapPath)
                        .WithArguments(new[]
                        {
                            "compose",
                            "exec",
                            "mongodb1",
                            "mongosh",
                            $"mongodb1:{MongoPort}",
                            "--eval",
                            "\"do { try { rs.status(); break; } catch (err) { rs.initiate({_id:'rs0',members:[{_id:0,host:'host.docker.internal:" + MongoPort.ToString() + "'}]}) } } while (true)\"",
                        })
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(this._console.WriteLine))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(this._console.WriteLine))
                        .ExecuteAsync(cancellationToken);
                    exitCode = result.ExitCode;
                }
                catch (Exception e)
                {
                    this._console.WriteException(e);
                    await Task.Delay(1000, cancellationToken);
                    exitCode = 1;
                }
            }
            while (exitCode != 0 && --nbRetry > 0);

            return result.ExitCode;
        }
    }
}
