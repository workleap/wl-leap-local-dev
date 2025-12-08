namespace Leap.Cli.Pipeline;

internal sealed class LeapException : Exception
{
    public LeapException(string? message)
        : base(message)
    {
    }

    public LeapException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}