using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Real.Graphics.Vulkan.Surface;

/// <summary>Dispatches to the platform-specific surface factory based on OS.</summary>
internal static class SurfaceFactory
{
    public static unsafe SurfaceKHR CreateForWindow(Vk vk, Instance instance, nint handle)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!vk.TryGetInstanceExtension<KhrWin32Surface>(instance, out var khrWin32Surface))
                throw new InvalidOperationException(
                    "VK_KHR_win32_surface is not enabled on this instance - check VulkanInstance's required extension list.");

            return Win32SurfaceFactory.Create(vk, instance, khrWin32Surface, handle);
        }

        if (OperatingSystem.IsLinux())
        {
            if (vk.TryGetInstanceExtension<KhrXlibSurface>(instance, out var khrXlibSurface))
                return XlibSurfaceFactory.Create(vk, instance, khrXlibSurface, handle);

            throw new PlatformNotSupportedException(
                "Neither VK_KHR_xlib_surface nor a Wayland surface extension is available on this instance. " +
                "If targeting Wayland, resolve KhrWaylandSurface here instead and branch on which native " +
                "handle fields NativeWindowHandle actually populated (X11Display/X11Window vs WaylandDisplay/WaylandSurface).");
        }

        throw new PlatformNotSupportedException("No Vulkan surface factory for this OS.");
    }
}
