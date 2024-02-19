using Milki.Extensions.Threading;

namespace UnlockFps.Logging;

public class ConsoleLogger(string name) : ILogger
{
    private readonly string _name = name;

    private static readonly SynchronizationContext LoggerSynchronizationContext =
        new SingleSynchronizationContext("Default ConsoleLogger");

    public void Log(LogLevel logLevel, string message)
    {
        LoggerSynchronizationContext.Post(_ =>
        {
            if (logLevel == LogLevel.Trace)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
            }
            else if (logLevel == LogLevel.Debug)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
            }
            else if (logLevel == LogLevel.Information)
            {
                Console.ForegroundColor = ConsoleColor.White;
            }
            else if (logLevel == LogLevel.Warning)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
            }
            else if (logLevel == LogLevel.Error)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }
            else if (logLevel == LogLevel.Critical)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.BackgroundColor = ConsoleColor.Red;
            }

            Console.Write($"[{DateTime.Now:HH:mm:ss.fff}] ");
            if (logLevel == LogLevel.Trace)
            {
                Console.Write("TRACE ");
            }
            else if (logLevel == LogLevel.Debug)
            {
                Console.Write("DEBUG ");
            }
            else if (logLevel == LogLevel.Information)
            {
                Console.Write("INFO ");
            }
            else if (logLevel == LogLevel.Warning)
            {
                Console.Write("WARN ");
            }
            else if (logLevel == LogLevel.Error)
            {
                Console.Write("ERROR ");
            }
            else if (logLevel == LogLevel.Critical)
            {
                Console.Write("CRITICAL ");
            }

            Console.Write($"{_name}: ");
            Console.WriteLine(message);
            Console.ResetColor();
        }, null);
    }

    public void LogInformation(string message)
    {
        Log(LogLevel.Information, message);
    }

    public void LogDebug(string message)
    {
        Log(LogLevel.Debug, message);
    }

    public void LogError(string message)
    {
        Log(LogLevel.Error, message);
    }

    public void LogWarning(string message)
    {
        Log(LogLevel.Warning, message);
    }

    public void LogInformation(Exception exception, string message)
    {
        Log(LogLevel.Information, message + "\r\n" + exception);
    }

    public void LogDebug(Exception exception, string message)
    {
        Log(LogLevel.Debug, message + "\r\n" + exception);
    }

    public void LogError(Exception exception, string message)
    {
        Log(LogLevel.Error, message + "\r\n" + exception);
    }

    public void LogWarning(Exception exception, string message)
    {
        Log(LogLevel.Warning, message + "\r\n" + exception);
    }
}