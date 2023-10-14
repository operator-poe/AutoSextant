namespace AutoSextant;

public enum LogLevel
{
    None,
    Error,
    Debug,
};

public class Log
{
    private static LogLevel LogLevel
    {
        get
        {
            return LogLevel.Debug;
            // switch (TujenMem.Instance.Settings.LogLevel)
            // {
            //     case "Debug":
            //         return LogLevel.Debug;
            //     case "Error":
            //         return LogLevel.Error;
            //     default:
            //         return LogLevel.None;
            // }
        }
    }
    public static void Debug(string message)
    {
        if (LogLevel < LogLevel.Debug)
            return;
        AutoSextant.Instance.LogMsg($"AutoSextant: {message}");
    }

    public static void Error(string message)
    {
        if (LogLevel < LogLevel.Error)
            return;
        AutoSextant.Instance.LogError($"AutoSextant: {message}");
    }

}