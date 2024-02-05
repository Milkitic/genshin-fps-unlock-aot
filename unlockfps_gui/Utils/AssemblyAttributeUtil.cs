using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace UnlockFps.Gui.Utils;

internal static class AssemblyAttributeUtil
{
    public static T? GetAssemblyAttribute<T>(out T? coreAttribute) where T : Attribute
    {
        var versionAsm = Assembly.GetCallingAssembly();
        var entryAsm = Assembly.GetEntryAssembly();
        var desiredEntryAsmName = versionAsm.GetName().Name?.Split('.')[0];
        var desiredEntryAsm =
            AssemblyLoadContext.Default.Assemblies.FirstOrDefault(k => k.GetName().Name == desiredEntryAsmName);
        if (desiredEntryAsm != null)
        {
            versionAsm = desiredEntryAsm;
        }

        if (entryAsm == versionAsm)
        {
            coreAttribute = null;
            return versionAsm.GetCustomAttribute<T>();
        }
        else
        {
            coreAttribute = versionAsm.GetCustomAttribute<T>();
            return entryAsm?.GetCustomAttribute<T>();
        }
    }
}