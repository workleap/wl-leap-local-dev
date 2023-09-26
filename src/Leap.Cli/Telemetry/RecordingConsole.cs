using System.CommandLine;
using System.CommandLine.IO;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Leap.Cli.Telemetry;

// Inspired from Spectre.Console's Recorder and TextEncoder:
// https://github.com/spectreconsole/spectre.console/blob/main/src/Spectre.Console/Recorder.cs
// https://github.com/spectreconsole/spectre.console/blob/0.47.0/src/Spectre.Console/Internal/Text/Encoding/TextEncoder.cs
internal sealed class RecordingConsole : IRecordingConsole, IConsole, IAnsiConsole
{
    private readonly StringBuilder _recorder;
    private readonly IConsole _underlyingConsole;
    private readonly IAnsiConsole _underlyingAnsiConsole;
    private readonly RenderOptions _recordingRenderOptions;

    public RecordingConsole()
        : this(new Utf8SystemConsole(), AnsiConsole.Console)
    {
    }

    public RecordingConsole(IConsole underlyingConsole, IAnsiConsole underlyingAnsiConsole)
    {
        this._recorder = new StringBuilder();
        this._underlyingConsole = underlyingConsole;
        this._underlyingAnsiConsole = underlyingAnsiConsole;
        this._recordingRenderOptions = RenderOptions.Create(this._underlyingAnsiConsole, new RecordingEncoderCapabilities(ColorSystem.TrueColor));

        this.Out = new RecordingStandardStreamWriter(underlyingConsole.Out, this._recorder);
        this.Error = new RecordingStandardStreamWriter(underlyingConsole.Error, this._recorder);
        this.Input = new RecordingAnsiConsoleInput(this._underlyingAnsiConsole.Input, this._recorder);
    }

    public IStandardStreamWriter Out { get; }

    public IStandardStreamWriter Error { get; }

    public bool IsOutputRedirected => this._underlyingConsole.IsOutputRedirected;

    public bool IsErrorRedirected => this._underlyingConsole.IsErrorRedirected;

    public bool IsInputRedirected => this._underlyingConsole.IsInputRedirected;

    public Profile Profile => this._underlyingAnsiConsole.Profile;

    public IAnsiConsoleCursor Cursor => this._underlyingAnsiConsole.Cursor;

    public IAnsiConsoleInput Input { get; }

    public IExclusivityMode ExclusivityMode => this._underlyingAnsiConsole.ExclusivityMode;

    public RenderPipeline Pipeline => this._underlyingAnsiConsole.Pipeline;

    public override string ToString()
    {
        return this._recorder.ToString().ReplaceLineEndings();
    }

    public void Clear(bool home)
    {
        this._underlyingAnsiConsole.Clear(home);
    }

    public void Write(IRenderable renderable)
    {
        var segments = renderable.Render(this._recordingRenderOptions, this._underlyingAnsiConsole.Profile.Width);

        foreach (var segment in segments)
        {
            if (segment.IsControlCode)
            {
                continue;
            }

            this._recorder.Append(segment.Text);
        }

        this._underlyingAnsiConsole.Write(renderable);
    }

    private sealed class RecordingStandardStreamWriter : IStandardStreamWriter
    {
        private readonly IStandardStreamWriter _underlyingStandardStreamWriter;
        private readonly StringBuilder _recorder;

        public RecordingStandardStreamWriter(IStandardStreamWriter underlyingStandardStreamWriter, StringBuilder recorder)
        {
            this._underlyingStandardStreamWriter = underlyingStandardStreamWriter;
            this._recorder = recorder;
        }

        public void Write(string? value)
        {
            if (value != null)
            {
                this._recorder.Append(value);
            }

            this._underlyingStandardStreamWriter.Write(value);
        }
    }

    private sealed class RecordingAnsiConsoleInput : IAnsiConsoleInput
    {
        // No need to record unnecessary control characters that are not actually printed to the console output,
        // also these characters are the only one used with interactive commands (text, single selection and multi selection prompts)
        private static readonly HashSet<ConsoleKey> RecordedControlConsoleKeys = new HashSet<ConsoleKey>
        {
            ConsoleKey.Enter,
            ConsoleKey.UpArrow,
            ConsoleKey.DownArrow,
        };

        private readonly IAnsiConsoleInput _underlyingAnsiConsoleInput;
        private readonly StringBuilder _recorder;

        public RecordingAnsiConsoleInput(IAnsiConsoleInput underlyingAnsiConsoleInput, StringBuilder recorder)
        {
            this._underlyingAnsiConsoleInput = underlyingAnsiConsoleInput;
            this._recorder = recorder;
        }

        public bool IsKeyAvailable()
        {
            return this._underlyingAnsiConsoleInput.IsKeyAvailable();
        }

        public ConsoleKeyInfo? ReadKey(bool intercept)
        {
            var consoleKeyInfo = this._underlyingAnsiConsoleInput.ReadKey(intercept);
            this.AppendConsoleKeyInfo(consoleKeyInfo);
            return consoleKeyInfo;
        }

        public async Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
        {
            var consoleKeyInfo = await this._underlyingAnsiConsoleInput.ReadKeyAsync(intercept, cancellationToken);
            this.AppendConsoleKeyInfo(consoleKeyInfo);
            return consoleKeyInfo;
        }

        private void AppendConsoleKeyInfo(ConsoleKeyInfo? consoleKeyInfo)
        {
            if (!consoleKeyInfo.HasValue)
            {
                return;
            }

            if (char.IsControl(consoleKeyInfo.Value.KeyChar))
            {
                if (RecordedControlConsoleKeys.Contains(consoleKeyInfo.Value.Key))
                {
                    this._recorder.AppendLine(consoleKeyInfo.Value.Key.ToString());
                }
            }
            else
            {
                this._recorder.Append(consoleKeyInfo.Value.KeyChar);
            }
        }
    }

    [ExcludeFromCodeCoverage]
    private sealed class RecordingEncoderCapabilities : IReadOnlyCapabilities
    {
        public RecordingEncoderCapabilities(ColorSystem colors)
        {
            this.ColorSystem = colors;
        }

        public ColorSystem ColorSystem { get; }

        public bool Ansi => false;

        public bool Links => false;

        public bool Legacy => false;

        public bool IsTerminal => false;

        public bool Interactive => false;

        public bool Unicode => true;
    }
}