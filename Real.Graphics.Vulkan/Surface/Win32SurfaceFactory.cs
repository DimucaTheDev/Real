using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Real.Graphics.Vulkan.Surface;

internal static class Win32SurfaceFactory
{
    public static unsafe SurfaceKHR Create(Vk vk, Instance instance, KhrWin32Surface khrWin32Surface,
        nint handle)
    {
        var createInfo = new Win32SurfaceCreateInfoKHR
        {
            SType = StructureType.Win32SurfaceCreateInfoKhr,
            Hwnd = handle,
            //Hinstance = handle.HInstance
        };

        if (khrWin32Surface.CreateWin32Surface(instance, in createInfo, null, out var khr) != Result.Success)
        {
            throw new Exception($"Failed to create Win32Surface: {khrWin32Surface}");
        }
        return khr;
    }
}