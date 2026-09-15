using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Real.Fmod;

internal static class FmodModuleInitializer
{
    [ModuleInitializer]
    internal static void Init()
    {
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), ResolveDll);
    }

    private static IntPtr ResolveDll(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        string extension = OperatingSystem.IsWindows() ? ".dll"
            : OperatingSystem.IsLinux() ? ".so"
            : ".dylib";

        string actualName = (!OperatingSystem.IsWindows() && !libraryName.StartsWith("lib"))
            ? $"lib{libraryName}"
            : libraryName;

        if (!actualName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            actualName += extension;
        }

        string nativePath = Path.Combine(AppContext.BaseDirectory, "engine", "natives", actualName);

        if (File.Exists(nativePath))
        {
            return NativeLibrary.Load(nativePath);
        } 
        
        return IntPtr.Zero;
    }
}