using System;
using System.Collections.Generic;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.Resources;

/// <summary>
/// Allocates VkDescriptorSets from a growable set of VkDescriptorPools.
/// Vulkan pools are fixed-size and don't grow, so this keeps a list of pools
/// and creates a new one on exhaustion rather than failing allocation.
/// </summary>
internal sealed class VulkanDescriptorAllocator : IDisposable
{
    public const int HeadlessFrameIndex = -1;

    // Fixed capacity per pool - tuned for a "few hundred draw calls worth of
    // materials" scene, not for a bindless design (which wouldn't need per-
    // draw descriptor sets at all).
    private const uint MaxSetsPerPool = 1000;
    private const uint UniformBuffersPerPool = 1000;
    private const uint CombinedImageSamplersPerPool = 1000;

    private sealed class FrameDescriptorPool
    {
        public readonly List<DescriptorPool> Pools = new();
        public int ActivePoolIndex;

        public unsafe void Reset(Vk vk, Device device)
        {
            for (int i = 0; i < Pools.Count; i++)
            {
                vk.ResetDescriptorPool(device, Pools[i], 0);
            }
            ActivePoolIndex = 0;
        }

        public unsafe void DestroyAll(Vk vk, Device device)
        {
            for (int i = 0; i < Pools.Count; i++)
            {
                if (Pools[i].Handle != 0)
                    vk.DestroyDescriptorPool(device, Pools[i], null);
            }
            Pools.Clear();
            ActivePoolIndex = 0;
        }
    }

    private readonly Vk _vk;
    private readonly Device _device;
    private readonly Dictionary<int, FrameDescriptorPool> _framePools = new();
    private int _currentFrame;

    public VulkanDescriptorAllocator(Vk vk, Device device)
    {
        _vk = vk;
        _device = device;
    }

    public void Dispose() => DestroyAll();

    public void SetCurrentFrame(int frameIndex)
    {
        _currentFrame = frameIndex;
    }

    public void ResetFrame(int frameIndex)
    {
        if (_framePools.TryGetValue(frameIndex, out var framePool))
        {
            framePool.Reset(_vk, _device);
        }
    }

    public unsafe DescriptorSet Allocate(DescriptorSetLayout layout)
    {
        var framePool = GetOrCreateFramePool(_currentFrame);

        if (framePool.Pools.Count == 0)
        {
            framePool.Pools.Add(CreatePool());
            framePool.ActivePoolIndex = 0;
        }

        while (true)
        {
            var currentPool = framePool.Pools[framePool.ActivePoolIndex];
            var result = TryAllocateFrom(currentPool, layout, out var set);

            if (result == Result.Success)
                return set;

            if (result is not (Result.ErrorOutOfPoolMemory or Result.ErrorFragmentedPool))
                throw new InvalidOperationException($"vkAllocateDescriptorSets failed: {result}");

            framePool.ActivePoolIndex++;
            if (framePool.ActivePoolIndex < framePool.Pools.Count)
            {
                continue;
            }

            var newPool = CreatePool();
            framePool.Pools.Add(newPool);
            result = TryAllocateFrom(newPool, layout, out set);

            if (result != Result.Success)
                throw new InvalidOperationException($"vkAllocateDescriptorSets failed even against a fresh pool: {result}");

            return set;
        }
    }

    private FrameDescriptorPool GetOrCreateFramePool(int frameIndex)
    {
        if (!_framePools.TryGetValue(frameIndex, out var pool))
        {
            pool = new FrameDescriptorPool();
            _framePools[frameIndex] = pool;
        }
        return pool;
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
        };

        var result = _vk.CreateDescriptorPool(_device, in createInfo, null, out var pool);
        if (result != Result.Success)
            throw new InvalidOperationException($"vkCreateDescriptorPool failed: {result}");

        return pool;
    }

    public unsafe void DestroyAll()
    {
        foreach (var framePool in _framePools.Values)
            framePool.DestroyAll(_vk, _device);
        _framePools.Clear();
    }
}