using System;
using System.Collections.Generic;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.Core;

/// <summary>
/// Owns the VkInstance. Responsible for enumerating and enabling
/// platform-required extensions (surface extensions) and, optionally,
/// validation layers.
/// </summary>
internal sealed class VulkanInstance : IDisposable
{
    public Vk Vk { get; }
    public Instance Handle { get; private set; }
    public bool ValidationEnabled { get; }

    public unsafe VulkanInstance(string appName, bool enableValidation)
    {
        Vk = Vk.GetApi();
        ValidationEnabled = enableValidation;

        var requiredExtensions = GetRequiredInstanceExtensions(enableValidation);
        var layers = enableValidation
            ? new[] { "VK_LAYER_KHRONOS_validation" }
            : Array.Empty<string>();

        var appInfoCreation = new ApplicationInfo()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)SilkMarshal.StringToPtr(appName),
            ApiVersion = Vk.Version13,
            ApplicationVersion = 0,
            EngineVersion = Vk.MakeVersion(1, 0)
        };
        var debug = new DebugUtilsMessengerCreateInfoEXT()
        {
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.ValidationBitExt,
            PfnUserCallback = new PfnDebugUtilsMessengerCallbackEXT((_, _, data, _) =>
            {
                Console.WriteLine("Validation layer: " + SilkMarshal.PtrToString((nint)data->PMessage));
                return Vk.False;
            }),
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            PUserData = null
        };
        var pLayers = (byte**)SilkMarshal.StringArrayToPtr(layers);
        var pExtensions = (byte**)SilkMarshal.StringArrayToPtr(requiredExtensions);
        try
        {
            var instanceCreateInfo = new InstanceCreateInfo()
            {
                PApplicationInfo = &appInfoCreation,
                EnabledLayerCount = (uint)layers.Length,
                SType = StructureType.InstanceCreateInfo,
                PpEnabledLayerNames = pLayers,
                EnabledExtensionCount = (uint)requiredExtensions.Count,
                PpEnabledExtensionNames = pExtensions,
                PNext = enableValidation ? &debug : null
            };

            if (Vk.CreateInstance(in instanceCreateInfo, null, out var instance) != Result.Success)
            {
                throw new Exception("Unable to create Vulkan instance");
            }

            Handle = instance;
        }
        finally
        {
            SilkMarshal.Free((nint)appInfoCreation.PApplicationName);
            SilkMarshal.Free((nint)pLayers);
            SilkMarshal.Free((nint)pExtensions);
        }
    }

    private static unsafe List<string> GetRequiredInstanceExtensions(bool enableValidation)
    {
        var extensions = new List<string>();

        try
        {
            var glfw = Silk.NET.GLFW.Glfw.GetApi();
            var glfwExts = glfw.GetRequiredInstanceExtensions(out var count);
            if (count > 0 && glfwExts != null)
            {
                for (uint i = 0; i < count; i++)
                {
                    var ext = SilkMarshal.PtrToString((nint)glfwExts[i]);
                    if (!string.IsNullOrEmpty(ext) && !extensions.Contains(ext))
                        extensions.Add(ext);
                }
            }
        }
        catch
        {
            // GLFW not initialized
        }

        if (!extensions.Contains("VK_KHR_surface"))
            extensions.Add("VK_KHR_surface");

        var osExt = OperatingSystem.IsWindows() ? "VK_KHR_win32_surface" : "VK_KHR_xlib_surface";
        if (!extensions.Contains(osExt))
            extensions.Add(osExt);

        if (enableValidation && !extensions.Contains("VK_EXT_debug_utils"))
            extensions.Add("VK_EXT_debug_utils");

        return extensions;
    }

    public unsafe void Dispose()
    {
        Vk.DestroyInstance(Handle, null);
        Vk.Dispose();
    }
}
