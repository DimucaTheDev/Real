using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Real.Graphics.Vulkan.Core;

/// <summary>
/// Owns the VkDevice created from a chosen VkPhysicalDevice, and resolves
/// the VulkanQueueSet used by the rest of the backend.
/// </summary>
internal sealed class VulkanLogicalDevice : IDisposable
{
    private static readonly string[] RequiredDeviceExtensions = { KhrSwapchain.ExtensionName };

    public Vk Vk { get; }
    public PhysicalDevice PhysicalDevice { get; }
    public Device Handle { get; private set; }
    public VulkanQueueSet Queues { get; private set; } = null!;

    public unsafe VulkanLogicalDevice(Vk vk, Instance instance, PhysicalDevice physicalDevice, SurfaceKHR surface)
    {
        Vk = vk;
        PhysicalDevice = physicalDevice;

        if (!vk.TryGetInstanceExtension<KhrSurface>(instance, out var khrSurface))
            throw new InvalidOperationException("VK_KHR_surface is not available on this instance.");

        var (graphicsFamily, presentFamily, transferFamily) =
            FindQueueFamilies(vk, physicalDevice, khrSurface, surface);

        var uniqueFamilies = new HashSet<uint> { graphicsFamily, presentFamily, transferFamily };
        var queueCreateInfos = new DeviceQueueCreateInfo[uniqueFamilies.Count];
        float queuePriority = 1.0f;

        int qi = 0;
        foreach (var family in uniqueFamilies)
        {
            queueCreateInfos[qi++] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = family,
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };
        }

        var enabledExtensionNames = SilkMarshal.StringArrayToPtr(RequiredDeviceExtensions);

        var deviceFeatures = new PhysicalDeviceFeatures();

        fixed (DeviceQueueCreateInfo* pQueueCreateInfos = queueCreateInfos)
        {
            var deviceCreateInfo = new DeviceCreateInfo
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = (uint)queueCreateInfos.Length,
                PQueueCreateInfos = pQueueCreateInfos,
                PEnabledFeatures = &deviceFeatures,
                EnabledExtensionCount = (uint)RequiredDeviceExtensions.Length,
                PpEnabledExtensionNames = (byte**)enabledExtensionNames,
                EnabledLayerCount = 0
            };

            var result = vk.CreateDevice(physicalDevice, in deviceCreateInfo, null, out var device);
            if (result != Result.Success)
                throw new InvalidOperationException($"vkCreateDevice failed: {result}");

            Handle = device;
        }

        SilkMarshal.Free(enabledExtensionNames);

        vk.GetDeviceQueue(Handle, graphicsFamily, 0, out var graphicsQueue);
        vk.GetDeviceQueue(Handle, presentFamily, 0, out var presentQueue);
        vk.GetDeviceQueue(Handle, transferFamily, 0, out var transferQueue);

        Queues = new VulkanQueueSet
        {
            GraphicsFamilyIndex = graphicsFamily,
            PresentFamilyIndex = presentFamily,
            TransferFamilyIndex = transferFamily,
            GraphicsQueue = graphicsQueue,
            PresentQueue = presentQueue,
            TransferQueue = transferQueue
        };
    }

    private static unsafe (uint Graphics, uint Present, uint Transfer) FindQueueFamilies(
        Vk vk, PhysicalDevice physicalDevice, KhrSurface khrSurface, SurfaceKHR surface)
    {
        uint familyCount = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref familyCount, null);

        var families = new QueueFamilyProperties[familyCount];
        fixed (QueueFamilyProperties* pFamilies = families)
            vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref familyCount, pFamilies);

        uint? graphics = null;
        uint? present = null;
        uint? transferOnly = null;

        for (uint i = 0; i < families.Length; i++)
        {
            var flags = families[i].QueueFlags;

            if (flags.HasFlag(QueueFlags.GraphicsBit) && graphics is null)
                graphics = i;

            if (flags.HasFlag(QueueFlags.TransferBit) && !flags.HasFlag(QueueFlags.GraphicsBit) && transferOnly is null)
                transferOnly = i;

            khrSurface.GetPhysicalDeviceSurfaceSupport(physicalDevice, i, surface, out var presentSupport);
            if (presentSupport && present is null)
                present = i;

            if (graphics is not null && present is not null && transferOnly is not null)
                break;
        }

        if (graphics is null)
            throw new InvalidOperationException("No queue family with VK_QUEUE_GRAPHICS_BIT found.");
        if (present is null)
            throw new InvalidOperationException("No queue family supports presenting to the given surface.");

        uint transfer = transferOnly ?? graphics.Value;

        return (graphics.Value, present.Value, transfer);
    }

    public unsafe void Dispose()
    {
        if (Handle.Handle != 0)
            Vk.DestroyDevice(Handle, null);
    }
}