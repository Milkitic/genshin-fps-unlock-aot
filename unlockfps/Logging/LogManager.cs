using System.Diagnostics;

namespace UnlockFps.Logging;

public static class LogManager
{
    private static ILoggerFactory? _loggerFactory;
    public static void SetLoggerFactory(ILoggerFactory loggerFactory) => _loggerFactory = loggerFactory;
    public static ILogger GetLogger(string name) => _loggerFactory?.CreateLogger(name) ?? new ConsoleLogger(name);
    //public static void Info(string message)
    //{
    //    var name = GetClassName() ?? nameof(Logger);
    //    if (_loggerFactory is null) Console.WriteLine(message);
    //    else _loggerFactory.CreateLogger(name).LogInformation(message);
    //}

    //public static void Debug(string message)
    //{
    //    var name = GetClassName() ?? nameof(Logger);
    //    if (_loggerFactory is null) Console.WriteLine(message);
    //    else _loggerFactory.CreateLogger(name).LogDebug(message);
    //}

    //public static void Error(string message)
    //{
    //    var name = GetClassName() ?? nameof(Logger);
    //    if (_loggerFactory is null) Console.WriteLine(message);
    //    else _loggerFactory.CreateLogger(name).LogError(message);
    //}

    //public static void Warn(string message)
    //{
    //    var name = GetClassName() ?? nameof(Logger);
    //    if (_loggerFactory is null) Console.WriteLine(message);
    //    else _loggerFactory.CreateLogger(name).LogWarning(message);
    //}

    //private static string? GetClassName()
    //{
    //    var methodInfo = new StackTrace().GetFrame(2)?.GetMethod();
    //    return methodInfo?.ReflectedType?.Name;
    //}
}