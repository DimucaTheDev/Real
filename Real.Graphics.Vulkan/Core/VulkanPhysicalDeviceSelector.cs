using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Real.Graphics.Vulkan.Core;

/// <summary>
/// Picks a VkPhysicalDevice suitable for rendering to the given surface:
/// discrete GPU preferred, must support graphics + presentation queues
/// and the swapchain extension.
/// </summary>
internal static class VulkanPhysicalDeviceSelector
{
    private static Vk _vk;

    public static PhysicalDevice Select(VulkanInstance instance, SurfaceKHR surface)
    {
        _vk = instance.Vk;
        var devices = GetPhysicalDevices(instance.Handle);

        if (!devices.Any())
            throw new Exception("No Vulkan devices found");

        foreach (var dev in GetPhysicalDevices(instance.Handle))
        {
            var supports = GetDeviceExtensions(dev).Contains(KhrSwapchain.ExtensionName);
            List<int> physDeviceGraphicFamilyIndices = [];
            List<int> physDevicePresentationFamilyIndices = [];
            var props = GetDeviceQueueProps(dev);
            for (int i = 0; i < props.Length; i++)
            {
                if ((props[i].QueueFlags & QueueFlags.GraphicsBit) != 0)
                    physDeviceGraphicFamilyIndices.Add(i);
                physDevicePresentationFamilyIndices.Add(i);
                i++;
            }

            if (supports && physDevicePresentationFamilyIndices.Any() && physDeviceGraphicFamilyIndices.Any())
            {
                return dev;
            }
        }

        throw new Exception("No suitable Vulkan devices found");
    }

    static unsafe PhysicalDevice[] GetPhysicalDevices(Instance instance)
    {
        uint count = 0;

        _vk.EnumeratePhysicalDevices(instance, &count, null);

        var properties = new PhysicalDevice[count];

        fixed (PhysicalDevice* ptr = properties)
        {
            _vk.EnumeratePhysicalDevices(instance, &count, ptr);
        }

        return properties
            .Take((int)count)
            .ToArray();
    }

    static unsafe QueueFamilyProperties[] GetDeviceQueueProps(PhysicalDevice physicalDevice)
    {
        uint count = 0;

        _vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &count, null);

        var properties = new QueueFamilyProperties[count];

        fixed (QueueFamilyProperties* ptr = properties)
        {
            _vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &count, ptr);
        }

        return properties
            .Take((int)count)
            .ToArray();
    }

    static unsafe string[] GetDeviceExtensions(PhysicalDevice physicalDevice)
    {
        uint count = 0;

        _vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &count, null);

        var properties = new ExtensionProperties[count];

        fixed (ExtensionProperties* ptr = properties)
        {
            _vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &count, ptr);
        }

        return properties
            .Take((int)count)
            .Select(x => SilkMarshal.PtrToString((nint)x.ExtensionName)!)
            .ToArray();
    }
}