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
                    CreateProcessWithMonitor();
                }
            });
    }

    private static void CreateMonitorOnly()
    {
        var configService = new ConfigService();
        configService.Save();

        using var cts = new CancellationTokenSource();
        var processScanner = new FpsOverrideDaemon(configService);
        Console.CancelKeyPress += (_, e) =>
        {
            processScanner.Stop();
            Environment.Exit(0);
        };
        Logger.LogInformation("Monitor mode. Press 'Ctrl+C' to exit.");
        processScanner.Start();
        while (Console.ReadLine() != "exit")
        {

        }

        processScanner.Stop();
        Environment.Exit(0);
    }

    private static void CreateProcessWithMonitor()
    {
        var configService = new ConfigService();
        configService.Save();

        using var cts = new CancellationTokenSource();
        var processScanner = new FpsOverrideDaemon(configService);
        Console.CancelKeyPress += (_, e) =>
        {
            processScanner.Stop();
            Environment.Exit(0);
        };
        processScanner.Start();
        while (Console.ReadLine() != "exit")
        {

        }

        processScanner.Stop();
        Environment.Exit(0);
    }
}