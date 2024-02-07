using System.Diagnostics.CodeAnalysis;
using CommandLine;

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
            .WithParsedAsync<Options>(async o =>
            {
                await CreateMonitorOnly();
                //if (o.MonitorOnly)
                //{
                //    await CreateMonitorOnly();
                //}
            });

    }

    private static async ValueTask CreateMonitorOnly()
    {
        using var cts = new CancellationTokenSource();
        var processScanner = new ProcessScanner(new Config()
        {
            FPSTarget = 120,
            UsePowerSave = true,
            GamePath = @"E:\其他文件\Genshin Impact\Genshin Impact Game\YuanShen.exe"
        });
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        Console.WriteLine("Monitor mode. Press 'Ctrl+C' to exit.");
        await processScanner.RunAsync(cts.Token);
    }
}