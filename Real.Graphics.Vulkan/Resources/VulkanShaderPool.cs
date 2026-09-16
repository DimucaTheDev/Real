using System;
using System.Collections.Generic;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.Resources;

internal readonly record struct VulkanShaderEntry(
    ShaderModule Module,
    ShaderStage Stage,
    string EntryPoint);

internal sealed class VulkanShaderPool : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly List<VulkanShaderEntry?> _slots = new();
    private readonly List<uint> _generations = new();
    private readonly Queue<uint> _freeSlots = new();

    public VulkanShaderPool(Vk vk, Device device)
    {
        _vk = vk;
        _device = device;

        // Reserve slot 0 so valid handles always have Id > 0 (ShaderHandle.Invalid is (0, 0))
        _slots.Add(null);
        _generations.Add(0);
    }

    public ShaderHandle Store(ShaderModule module, ShaderStage stage, string entryPoint)
    {
        uint id;
        if (_freeSlots.Count > 0)
        {
            id = _freeSlots.Dequeue();
            _slots[(int)id] = new VulkanShaderEntry(module, stage, entryPoint);
        }
        else
        {
            id = (uint)_slots.Count;
            _slots.Add(new VulkanShaderEntry(module, stage, entryPoint));
            _generations.Add(0);
        }

        return new ShaderHandle(id, _generations[(int)id]);
    }

    public bool IsValid(ShaderHandle handle) =>
        handle.Id != 0 &&
        handle.Id < _slots.Count &&
        _slots[(int)handle.Id] is not null &&
        _generations[(int)handle.Id] == handle.Generation;

    public VulkanShaderEntry Get(ShaderHandle handle)
    {
        if (!IsValid(handle))
            throw new ArgumentException($"Invalid or expired shader handle: {handle}");
        return _slots[(int)handle.Id]!.Value;
    }

    public unsafe void Destroy(ShaderHandle handle)
    {
        if (!IsValid(handle)) return;

        var entry = _slots[(int)handle.Id]!.Value;
        _vk.DestroyShaderModule(_device, entry.Module, null);

        _slots[(int)handle.Id] = null;
        _generations[(int)handle.Id]++;
        _freeSlots.Enqueue(handle.Id);
    }

    public unsafe void Dispose()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] is { } entry)
            {
                _vk.DestroyShaderModule(_device, entry.Module, null);
                _slots[i] = null;
            }
        }
        _freeSlots.Clear();
    }
}
