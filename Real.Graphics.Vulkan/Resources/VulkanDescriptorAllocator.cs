using System;
using System.Collections.Generic;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.Resources;

/// <summary>
/// Allocates VkDescriptorSets from a growable set of VkDescriptorPools.
/// Vulkan pools are fixed-size and don't grow, so this keeps a list of pools
/// and creates a new one on exhaustion rather than failing allocation.
/// </summary>
internal sealed class VulkanDescriptorAllocator
{
    // Fixed capacity per pool - tuned for a "few hundred draw calls worth of
    // materials" scene, not for a bindless design (which wouldn't need per-
    // draw descriptor sets at all). Bump these if CreatePool starts getting
    // called often - that's a sign real usage outgrew this guess.
    private const uint MaxSetsPerPool = 1000;
    private const uint UniformBuffersPerPool = 1000;
    private const uint CombinedImageSamplersPerPool = 1000;

    private readonly Vk _vk;
    private readonly Device _device;
    private readonly List<DescriptorPool> _pools = new();

    public VulkanDescriptorAllocator(Vk vk, Device device)
    {
        _vk = vk;
        _device = device;
    }

    public unsafe DescriptorSet Allocate(DescriptorSetLayout layout)
    {
        if (_pools.Count == 0)
            CreatePool();

        var currentPool = _pools[^1];
        var result = TryAllocateFrom(currentPool, layout, out var set);

        if (result == Result.Success)
            return set;

        if (result is not (Result.ErrorOutOfPoolMemory or Result.ErrorFragmentedPool))
            throw new InvalidOperationException($"vkAllocateDescriptorSets failed: {result}");

        // Current pool is exhausted/fragmented - Vulkan pools don't grow or
        // defragment, so the only way forward is a fresh pool.
        var newPool = CreatePool();
        result = TryAllocateFrom(newPool, layout, out set);

        if (result != Result.Success)
            throw new InvalidOperationException($"vkAllocateDescriptorSets failed even against a fresh pool: {result}");

        return set;
    }

    private unsafe Result TryAllocateFrom(DescriptorPool pool, DescriptorSetLayout layout, out DescriptorSet set)
    {
        var layoutCopy = layout; // AllocateInfo needs a pointer to a stable local
        var allocInfo = new DescriptorSetAllocateInfo
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = pool,
            DescriptorSetCount = 1,
            PSetLayouts = &layoutCopy
        };

        return _vk.AllocateDescriptorSets(_device, in allocInfo, out set);
    }

    private unsafe DescriptorPool CreatePool()
    {
        var poolSizes = stackalloc DescriptorPoolSize[]
        {
            new DescriptorPoolSize
            {
                Type = DescriptorType.UniformBuffer,
                DescriptorCount = UniformBuffersPerPool
            },
            new DescriptorPoolSize
            {
                Type = DescriptorType.CombinedImageSampler,
                DescriptorCount = CombinedImageSamplersPerPool
            }
        };

        var createInfo = new DescriptorPoolCreateInfo
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = 2,
            PPoolSizes = poolSizes,
            MaxSets = MaxSetsPerPool
            // No FreeDescriptorSetBit: we never free individual sets, only ever
            // retire a whole pool (see DestroyAll below) - matches the render
            // graph's per-frame descriptor update model, not per-draw churn.
        };

        var result = _vk.CreateDescriptorPool(_device, in createInfo, null, out var pool);
        if (result != Result.Success)
            throw new InvalidOperationException($"vkCreateDescriptorPool failed: {result}");

        _pools.Add(pool);
        return pool;
    }

    public unsafe void DestroyAll()
    {
        foreach (var pool in _pools)
            _vk.DestroyDescriptorPool(_device, pool, null);
        _pools.Clear();
    }
}