using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Real.Fmod;

internal static class FmodModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        //its needed to laod lib.so before its tries to load lib.so.14, which doesnt exist 
        NativeLibrary.SetDllImportResolver(typeof(FmodModuleInitializer).Assembly, Resolve);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "fmod" && libraryName != "fmodstudio")
            return IntPtr.Zero;

        string baseDir = AppContext.BaseDirectory;
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        string coreFile = FindExisting(baseDir, isWindows ? "fmod.dll" : "libfmod.so",
            isWindows ? "fmodL.dll" : "libfmodL.so");
 
        IntPtr coreHandle = NativeLibrary.Load(coreFile);

        if (libraryName == "fmod")
            return coreHandle;

        string studioFile = FindExisting(baseDir, isWindows ? "fmodstudio.dll" : "libfmodstudio.so",
            isWindows ? "fmodstudioL.dll" : "libfmodstudioL.so");
        return NativeLibrary.Load(studioFile);
    }

    private static string FindExisting(string baseDir, string release, string debug)
    {
        string releasePath = Path.Combine(baseDir, release);
        string debugPath = Path.Combine(baseDir, debug);
        if (File.Exists(releasePath)) return releasePath;
        if (File.Exists(debugPath)) return debugPath;
        throw new DllNotFoundException($"Не найден ни {release}, ни {debug} в {baseDir}");
    }
}