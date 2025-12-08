using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Leap.Cli.Model;

internal sealed class AppSettingsJsonManager : IAppSettingsJsonManager
{
    private static readonly JsonSerializerOptions AppSettingsSerializerOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
    };

    private readonly IFileSystem _fileSystem;

    public AppSettingsJsonManager(IFileSystem fileSystem)
    {
        this._fileSystem = fileSystem;

        this.Configuration = new JsonObject();
    }

    public JsonObject Configuration { get; }

    public async Task WriteUpdatedAppSettingsJson(CancellationToken cancellationToken)
    {
        var appSettingsJsonFilePath = Constants.LeapAppSettingsFilePath;

        await using var stream = this._fileSystem.File.Create(appSettingsJsonFilePath);
        await using var streamWriter = new StreamWriter(stream);

        await streamWriter.WriteAsync(this.Configuration.ToJsonString(AppSettingsSerializerOptions));
    }
}