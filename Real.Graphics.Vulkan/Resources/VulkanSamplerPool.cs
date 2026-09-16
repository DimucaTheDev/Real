using System.Collections.Generic;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Handles;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.Resources;

/// <summary>
/// Same slot-pool pattern as VulkanShaderPool - owns VkSampler creation from a
/// SamplerDescriptor and maps it to the opaque SamplerHandle exposed by the RHI.
/// </summary>
internal sealed class VulkanSamplerPool : System.IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;

    private readonly List<Sampler?> _slots = new();
    private readonly List<uint> _generations = new();
    private readonly Queue<uint> _freeSlots = new();

    public VulkanSamplerPool(Vk vk, Device device)
    {
        _vk = vk;
        _device = device;

        // Reserve slot 0 so valid handles always have Id > 0 (SamplerHandle.Invalid is (0, 0))
        _slots.Add(null);
        _generations.Add(0);
    }

    public unsafe void Dispose()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] is { } sampler)
            {
                _vk.DestroySampler(_device, sampler, null);
                _slots[i] = null;
            }
        }
        _freeSlots.Clear();
    }

    public unsafe SamplerHandle Create(in SamplerDescriptor descriptor)
    {
        var createInfo = new SamplerCreateInfo
        {
            SType = StructureType.SamplerCreateInfo,
            MinFilter = ToVkFilter(descriptor.MinFilter),
            MagFilter = ToVkFilter(descriptor.MagFilter),
            AddressModeU = ToVkAddressMode(descriptor.AddressU),
            AddressModeV = ToVkAddressMode(descriptor.AddressV),
            // No AddressW on SamplerDescriptor (it's 2D-texture-oriented) -
            // repeat matches AddressU/V's own default and is a safe no-op for
            // every 2D sampling case, which is all this RHI currently supports.
            AddressModeW = ToVkAddressMode(descriptor.AddressU),
            AnisotropyEnable = descriptor.MaxAnisotropy > 1.0f,
            MaxAnisotropy = descriptor.MaxAnisotropy,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            MipmapMode = descriptor.MinFilter == FilterMode.Linear
                ? SamplerMipmapMode.Linear
                : SamplerMipmapMode.Nearest,
            MinLod = 0,
            MaxLod = Vk.LodClampNone // let the sampler see every mip the image view exposes
        };

        var result = _vk.CreateSampler(_device, in createInfo, null, out var sampler);
        if (result != Result.Success)
            throw new System.InvalidOperationException($"vkCreateSampler failed: {result}");

        return Store(sampler);
    }

    private static Filter ToVkFilter(FilterMode mode) => mode switch
    {
        FilterMode.Nearest => Filter.Nearest,
        FilterMode.Linear => Filter.Linear,
        _ => Filter.Linear
    };

    private static SamplerAddressMode ToVkAddressMode(AddressMode mode) => mode switch
    {
        AddressMode.Repeat => SamplerAddressMode.Repeat,
        AddressMode.ClampToEdge => SamplerAddressMode.ClampToEdge,
        AddressMode.MirroredRepeat => SamplerAddressMode.MirroredRepeat,
        _ => SamplerAddressMode.Repeat
    };

    public bool IsValid(SamplerHandle handle) =>
        handle.Id != 0 &&
        handle.Id < _slots.Count &&
        _slots[(int)handle.Id] is not null &&
        _generations[(int)handle.Id] == handle.Generation;

    public Sampler Get(SamplerHandle handle) => _slots[(int)handle.Id]!.Value;

    public unsafe void Destroy(SamplerHandle handle)
    {
        if (!IsValid(handle)) return;

        _vk.DestroySampler(_device, _slots[(int)handle.Id]!.Value, null);

        _slots[(int)handle.Id] = null;
        _generations[(int)handle.Id]++;
        _freeSlots.Enqueue(handle.Id);
    }

    private SamplerHandle Store(Sampler sampler)
    {
        uint id;
        if (_freeSlots.Count > 0)
        {
            id = _freeSlots.Dequeue();
            _slots[(int)id] = sampler;
        }
        else
        {
            id = (uint)_slots.Count;
            _slots.Add(sampler);
            _generations.Add(0);
        }

        return new SamplerHandle(id, _generations[(int)id]);
    }
}