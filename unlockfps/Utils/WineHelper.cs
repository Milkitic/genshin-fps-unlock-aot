using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using UnlockFps.Logging;

namespace UnlockFps.Utils;

internal static partial class WineHelper
{
    private static readonly ILogger Logger = LogManager.GetLogger(nameof(WineHelper));

    public static bool DetectWine(
        [NotNullWhen(true)] out string? version,
        [NotNullWhen(true)] out string? buildId)
    {
        try
        {
            version = GetVersion();
            Logger.LogInformation($"Wine version: {version}");
            buildId = GetBuildId();
            Logger.LogInformation($"Wine build id: {buildId}");
            return true;
        }
        catch (EntryPointNotFoundException)
        {
            version = null;
            buildId = null;
            return false;
        }
        catch (DllNotFoundException)
        {
            version = null;
            buildId = null;
            return false;
        }
    }

    [LibraryImport("ntdll", EntryPoint = "wine_get_version", StringMarshalling = StringMarshalling.Utf8)]
    private static partial string GetVersion();

    [LibraryImport("ntdll", EntryPoint = "wine_get_build_id", StringMarshalling = StringMarshalling.Utf8)]
    private static partial string GetBuildId();
}