using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Real.Graphics.Vulkan.Surface;

internal static class XlibSurfaceFactory
{
    public static unsafe SurfaceKHR Create(Vk vk, Instance instance, KhrXlibSurface khrXlibSurface, nint handle)
    {
        var createInfo = new XlibSurfaceCreateInfoKHR
        {
            SType = StructureType.XlibSurfaceCreateInfoKhr,
            //Dpy = (nint*)handle.X11Display,
            Window = handle
        };

        if (khrXlibSurface.CreateXlibSurface(instance, in createInfo, null, out var khr) != Result.Success)
        {
            return khr;
        }
        throw new Exception($"Failed to create XlibSurface: {khrXlibSurface}");
    }
}
