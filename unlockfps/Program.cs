using System.Diagnostics.CodeAnalysis;
using CommandLine;
using Microsoft.Extensions.Logging;
using UnlockFps.Services;
using UnlockFps.Utils;

namespace UnlockFps;

internal class Program
{
    public class Options
    {
        [Option('m', "monitor-only", Default = false, Required = false)]
        public bool MonitorOnly { get; set; }
    }

    private static readonly ILogger Logger = LogUtils.GetLogger(nameof(Program));

    [STAThread]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Options))]
    static async Task Main(string[] args)
    {
        LogUtils.LoggerFactory = LoggerFactory.Create(builder => builder
            .AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "[HH:mm:ss.fff] ";
            })
            .AddFilter(_ => true)
        );
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
        var fpsDaemon = new FpsDaemon(configService);
        Console.CancelKeyPress += (_, e) =>
        {
            Exit(fpsDaemon);
        };
        Logger.LogInformation("Monitor mode. Press 'Ctrl+C' to exit.");
        fpsDaemon.Start();
        while (Console.ReadLine() != "exit")
        {

        }

        Exit(fpsDaemon);
    }

    private static async ValueTask CreateProcessWithMonitor()
    {
        var configService = new ConfigService();
        configService.Save();

        using var cts = new CancellationTokenSource();
        var fpsDaemon = new FpsDaemon(configService);
        fpsDaemon.Start();

        var processService = new ProcessService(configService);
        processService.Start();

        fpsDaemon.ProcessExit += (p) =>
        {
            Exit(fpsDaemon);
        };
        Console.CancelKeyPress += (_, e) =>
        {
            processService.KillLastProcess();
            Exit(fpsDaemon);
        };


        while (Console.ReadLine() != "exit")
        {

        }

        processService.KillLastProcess();
        Exit(fpsDaemon);
    }

    private static void Exit(FpsDaemon fpsDaemon)
    {
        fpsDaemon.Stop();
        Environment.Exit(0);
    }
}