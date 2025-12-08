namespace Leap.StartupHook;

public static class PersistentDotnetExecutableDebuggingState
{
    public static void EnableDebugging(string debuggingSignalFilePath)
    {
        try
        {
            File.WriteAllText(debuggingSignalFilePath, string.Empty);
        }
        catch (Exception)
        {
            // Ignored
        }
    }

    public static void DisableDebugging(string debuggingSignalFilePath)
    {
        try
        {
            File.Delete(debuggingSignalFilePath);
        }
        catch (Exception)
        {
            // Ignored
        }
    }

    public static bool IsDebuggingEnabled(string debuggingSignalFilePath)
    {
        return File.Exists(debuggingSignalFilePath);
    }
}