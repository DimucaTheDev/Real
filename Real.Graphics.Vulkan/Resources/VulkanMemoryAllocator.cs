using System;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.Resources;

/// <summary>
/// Thin wrapper responsible for device memory allocation/suballocation.
/// In a real implementation this typically wraps AMD's VMA (via P/Invoke or
/// VMA-Sharp bindings) rather than reimplementing a suballocator from scratch.
/// </summary>
internal sealed class VulkanMemoryAllocator
{
    private readonly Vk _vk;
    private readonly PhysicalDevice _physicalDevice;
    private readonly Device _device;

    public VulkanMemoryAllocator(Vk vk, PhysicalDevice physicalDevice, Device device)
    {
        _vk = vk;
        _physicalDevice = physicalDevice;
        _device = device;
    }

    public unsafe DeviceMemory Allocate(MemoryRequirements requirements, MemoryPropertyFlags properties)
    {
        uint memoryTypeIndex = FindMemoryTypeIndex(requirements.MemoryTypeBits, properties);

        var allocInfo = new MemoryAllocateInfo
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = requirements.Size,
            MemoryTypeIndex = memoryTypeIndex
        };

        var result = _vk.AllocateMemory(_device, in allocInfo, null, out var memory);
        if (result != Result.Success)
            throw new InvalidOperationException($"vkAllocateMemory failed: {result}");

        return memory;
    }

    public unsafe void Free(DeviceMemory memory) =>
        _vk.FreeMemory(_device, memory, null);

    private unsafe uint FindMemoryTypeIndex(uint typeFilter, MemoryPropertyFlags requiredProperties)
    {
        _vk.GetPhysicalDeviceMemoryProperties(_physicalDevice, out var memProperties);

        for (int i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            // typeFilter is a bitmask from VkMemoryRequirements - bit i set means
            // memory type i is compatible with this resource at all. On top of
            // that we require every flag in requiredProperties to be present
            // (HOST_VISIBLE, DEVICE_LOCAL, etc).
            bool isCompatibleType = (typeFilter & (1 << i)) != 0;
            bool hasRequiredProperties =
                (memProperties.MemoryTypes[i].PropertyFlags & requiredProperties) == requiredProperties;

            if (isCompatibleType && hasRequiredProperties)
                return (uint)i;
        }

        throw new InvalidOperationException(
            $"No memory type found matching typeFilter={typeFilter:X} and properties={requiredProperties}.");
    }
}