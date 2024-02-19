using System.Diagnostics.CodeAnalysis;
using CommandLine;
using UnlockFps.Logging;
using UnlockFps.Services;

namespace UnlockFps;

internal class Program
{
    public class Options
    {
        [Option('m', "monitor-only", Default = false, Required = false)]
        public bool MonitorOnly { get; set; }
    }

    private static readonly ILogger Logger = LogManager.GetLogger(nameof(Program));

    [STAThread]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Options))]
    static async Task Main(string[] args)
    {
        await Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(async o =>
            {
                if (o.MonitorOnly)
                {
                    CreateMonitorOnly();
                }
                else
                {
                    await CreateProcessWithMonitor();
                }
            });
    }

    private static void CreateMonitorOnly()
    {
        var configService = new ConfigService();
        configService.Save();

        using var cts = new CancellationTokenSource();
        var gameInstanceService = new GameInstanceService(configService);
        Console.CancelKeyPress += (_, e) =>
        {
            Exit(gameInstanceService);
        };
        Logger.LogInformation("Monitor mode. Press 'Ctrl+C' to exit.");
        gameInstanceService.Start();
        while (Console.ReadLine() != "exit")
        {

        }

        Exit(gameInstanceService);
    }

    private static async ValueTask CreateProcessWithMonitor()
    {
        var configService = new ConfigService();
        configService.Save();

        using var cts = new CancellationTokenSource();
        var gameInstanceService = new GameInstanceService(configService);
        gameInstanceService.Start();

        var processService = new ProcessService(configService);
        processService.Start();

        gameInstanceService.ProcessExit += (p) =>
        {
            Exit(gameInstanceService);
        };
        Console.CancelKeyPress += (_, e) =>
        {
            processService.KillLastProcess();
            Exit(gameInstanceService);
        };


        while (Console.ReadLine() != "exit")
        {

        }

        processService.KillLastProcess();
        Exit(gameInstanceService);
    }

    private static void Exit(GameInstanceService gameInstanceService)
    {
        gameInstanceService.Stop();
        Environment.Exit(0);
    }
}