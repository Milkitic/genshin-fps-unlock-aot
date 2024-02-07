using System.Diagnostics.CodeAnalysis;
using CommandLine;
using UnlockFps.Services;

namespace UnlockFps;

internal class Program
{
    public class Options
    {
        [Option('m', "monitor-only", Default = false, Required = false)]
        public bool MonitorOnly { get; set; }
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Options))]
    static async Task Main(string[] args)
    {
        await Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(async o =>
            {
                if (o.MonitorOnly)
                {
                    await CreateMonitorOnly();
                }
            });

    }

    private static async ValueTask CreateMonitorOnly()
    {
        var configService = new ConfigService();
        configService.Save();

        using var cts = new CancellationTokenSource();
        var processScanner = new ProcessScanner(configService.Config);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        Console.WriteLine("Monitor mode. Press 'Ctrl+C' to exit.");
        await processScanner.RunAsync(cts.Token);
    }
}