using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;

namespace Real.Graphics.Vulkan.Core;

/// <summary>
/// Wraps VK_EXT_debug_utils. Only created when validation layers are enabled
/// (see VulkanDevice(window, enableValidation: true)).
/// </summary>
internal sealed class VulkanDebugMessenger : IDisposable
{
    private readonly Vk _vk;
    private readonly Instance _instance;
    private readonly ExtDebugUtils _debugUtils;
    private DebugUtilsMessengerEXT _messenger;

    public unsafe VulkanDebugMessenger(Vk vk, Instance instance, ExtDebugUtils debugUtils)
    {
        _vk = vk;
        _instance = instance;
        _debugUtils = debugUtils;

        var createInfo = new DebugUtilsMessengerCreateInfoEXT
        {
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt
                | DebugUtilsMessageSeverityFlagsEXT.WarningBitExt
                | DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt
                | DebugUtilsMessageTypeFlagsEXT.ValidationBitExt
                | DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt,
            PfnUserCallback = new PfnDebugUtilsMessengerCallbackEXT(DebugCallback)
        };
        _debugUtils.CreateDebugUtilsMessenger(_instance, &createInfo, null, out _messenger); 
    }

    private static unsafe uint DebugCallback(
        DebugUtilsMessageSeverityFlagsEXT severity,
        DebugUtilsMessageTypeFlagsEXT type,
        DebugUtilsMessengerCallbackDataEXT* data,
        void* userData)
    {
        var message = System.Runtime.InteropServices.Marshal.PtrToStringAnsi((nint)data->PMessage);
        Console.WriteLine($"[Vulkan][{severity}] {message}");
        return Vk.False;
    }

    public unsafe void Dispose()
    {
        _debugUtils.DestroyDebugUtilsMessenger(_instance, _messenger, null);
    }
}
