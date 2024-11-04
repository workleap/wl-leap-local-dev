using Leap.Cli.Pipeline;

namespace Leap.Cli.Dependencies;

internal sealed partial class MongoDependencyHandler
{
    private static readonly string ReplicaSetInitScriptContent =
        /*lang=js*/$$"""
        // Most of this script was AI-generated with OpenAI's O1-preview model.
        // Using the service name for the replica set primary member helps with name resolution (it always resolves to the container)
        const expectedHost = '{{ServiceName}}:{{MongoPort}}';
        const rsName = '{{ReplicaSetName}}';

        function waitForPrimary() {
          while (true) {
            try {
              const status = rs.status();
              // State 1 means "PRIMARY" in https://www.mongodb.com/docs/manual/reference/replica-states/#replica-set-member-states
              if (status.myState === 1 && status.members[0].name === expectedHost) {
                print('Replica set is initialized and primary is ready.');
                quit(0);
              }
            } catch (e) {
              // Replica set not yet initialized or in transition
            }
            sleep(1000);
          }
        }

        try {
          const config = rs.conf();
          const currentHost = config.members[0].host;

          if (currentHost === expectedHost) {
            print('Replica set already initialized with the correct host.');
            waitForPrimary();
          } else {
            // Leap local dev previously initialized the replica set with the host 'host.docker.internal'
            // We migrate the existing replica set to use the Docker Compose service name instead
            print('Replica set initialized with unexpected host "' + currentHost + '". Reconfiguring to "' + expectedHost + '".');
            config.members[0].host = expectedHost;
            rs.reconfig(config, { force: true });
            waitForPrimary();
          }
        } catch (e) {
          if (e.code === 94) { // Replica set not initialized
            print('Initializing new replica set "' + rsName + '" with host "' + expectedHost + '".');
            rs.initiate({
              _id: rsName,
              members: [{ _id: 0, host: expectedHost }]
            });
            waitForPrimary();
          } else {
            print('An error occurred: ' + e);
            quit(1);
          }
        }
        """;

    private static async Task WriteReplicaSetInitScriptAsync(CancellationToken cancellationToken)
    {
        try
        {
            await File.WriteAllTextAsync(ReplicaSetInitScriptHostFilePath, ReplicaSetInitScriptContent, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new LeapException($"An error occured while writing the MongoDB replica set init script at '{ReplicaSetInitScriptHostFilePath}'", ex);
        }
    }
}