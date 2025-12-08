using Leap.Cli.Platform;

namespace Leap.Cli.Commands;

internal sealed class AddCertificateAuthorityToComputerRootStoreCommand : Command<AddCertificateAuthorityToComputerRootStoreCommandOptions, AddCertificateAuthorityToComputerRootStoreCommandHandler>
{
    public const string CommandName = "addcatocomputerrootstore";

    public AddCertificateAuthorityToComputerRootStoreCommand()
        : base(CommandName, "Propagates the mkcert development certificate authority to the local computer's root store on Windows.")
    {
        // Internal command, not meant to be used directly
        this.IsHidden = true;
    }
}

internal sealed class AddCertificateAuthorityToComputerRootStoreCommandOptions : ICommandOptions;

internal sealed class AddCertificateAuthorityToComputerRootStoreCommandHandler(MkcertCertificateManager mkcertCertificateManager)
    : ICommandOptionsHandler<AddCertificateAuthorityToComputerRootStoreCommandOptions>
{
    public async Task<int> HandleAsync(AddCertificateAuthorityToComputerRootStoreCommandOptions options, CancellationToken cancellationToken)
    {
        await mkcertCertificateManager.EnsureCertificateAuthorityIsInstalledInComputerRootStoreAsync(cancellationToken);
        return 0;
    }
}