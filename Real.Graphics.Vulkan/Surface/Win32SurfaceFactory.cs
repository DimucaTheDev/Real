using System;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Real.Graphics.Vulkan.Surface;

internal static class Win32SurfaceFactory
{
    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("glfw3", CallingConvention = CallingConvention.Cdecl, EntryPoint = "glfwGetWin32Window")]
    private static extern nint glfwGetWin32Window(nint window);

    public static unsafe SurfaceKHR Create(Vk vk, Instance instance, KhrWin32Surface khrWin32Surface,
        nint handle)
    {
        nint hwnd = handle;
        try
        {
            if (!IsWindow(hwnd))
            {
                var glfwHwnd = glfwGetWin32Window(handle);
                if (glfwHwnd != 0 && IsWindow(glfwHwnd))
                    hwnd = glfwHwnd;
            }
        }
        catch
        {
            // Fallback if glfw3 isn't dynamically linked or export isn't found
        }

        var createInfo = new Win32SurfaceCreateInfoKHR
        {
            SType = StructureType.Win32SurfaceCreateInfoKhr,
            Hwnd = hwnd
        };

        if (khrWin32Surface.CreateWin32Surface(instance, in createInfo, null, out var khr) != Result.Success)
        {
            throw new Exception($"Failed to create Win32Surface: {khrWin32Surface}");
        }
        return khr;
    }
}
