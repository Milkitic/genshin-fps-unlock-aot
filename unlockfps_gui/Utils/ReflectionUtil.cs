using System.Reflection;
using Semver;

namespace UnlockFps.Gui.Utils;

internal static class ReflectionUtil
{
    private static string? _version;
    private static string? _company;

    public static string GetInformationalVersion()
    {
        if (_version != null) return _version;

        //var runner = AssemblyAttributeUtil.GetAssemblyAttribute<AssemblyInformationalVersionAttribute>(out var core);
        var runner = typeof(ReflectionUtil).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var core = default(AssemblyInformationalVersionAttribute);
        var runnerVersion = runner!.InformationalVersion;
        FixCommit(ref runnerVersion);

        if (core == null)
        {
            return _version ??= runnerVersion;
        }

        var coreVersion = core.InformationalVersion;
        FixCommit(ref coreVersion);
        if (coreVersion == runnerVersion)
        {
            return _version ??= runnerVersion;
        }

        return _version ??= $"{runnerVersion} (core: {coreVersion})";
    }

    public static string GetCompany()
    {
        if (_company != null) return _company;

        var runner = AssemblyAttributeUtil.GetAssemblyAttribute<AssemblyCompanyAttribute>(out var core);
        var runnerCompany = runner!.Company;
        if (core == null)
        {
            return _company ??= runnerCompany;
        }

        var coreCompany = core.Company;
        if (coreCompany == runnerCompany)
        {
            return _company ??= runnerCompany;
        }

        return _company ??= $"{runnerCompany} (core: {coreCompany})";
    }

    private static void FixCommit(ref string version)
    {
        if (!SemVersion.TryParse(version, SemVersionStyles.Strict, out var semVer)) return;

        if (!semVer.IsPrerelease)
        {
            var lastIndexOf = version.LastIndexOf('+');

            var lastIndexOfDot = version.LastIndexOf('.');
            var subStr = version.Substring(lastIndexOfDot + 1);
            if (subStr.Length == 40)
            {
                version = version.Substring(0, lastIndexOfDot);
            }
            else
            {
                version = version.Substring(0, lastIndexOf);
            }
        }
        else if (semVer.Metadata.Length > 7)
        {
            var lastIndexOf = version.LastIndexOf('+');
            version = version.Substring(0, lastIndexOf + 8);
        }
    }
}