using System.Collections.Generic;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.Resources;

internal readonly record struct VulkanShaderEntry(ShaderModule Module, ShaderStage Stage, string EntryPoint);

/// <summary>
/// Same slot-pool pattern as buffers/textures. Needed because CreateGraphicsPipelines
/// takes VkShaderModule handles directly - VulkanPipelinePool.Create must be able to
/// resolve a ShaderHandle back to its module.
/// </summary>
internal sealed class VulkanPipelinePool
{
    private readonly List<VulkanShaderEntry?> _slots = new();
    private readonly List<uint> _generations = new();
    private readonly Queue<uint> _freeSlots = new();

    public ShaderHandle Store(ShaderModule module, ShaderStage stage, string entryPoint)
    {
        var entry = new VulkanShaderEntry(module, stage, entryPoint);

        uint id;
        if (_freeSlots.Count > 0)
        {
            id = _freeSlots.Dequeue();
            _slots[(int)id] = entry;
        }
        else
        {
            id = (uint)_slots.Count;
            _slots.Add(entry);
            _generations.Add(0);
        }

        return new ShaderHandle(id, _generations[(int)id]);
    }

    public bool IsValid(ShaderHandle handle) =>
        handle.Id < _slots.Count &&
        _slots[(int)handle.Id] is not null &&
        _generations[(int)handle.Id] == handle.Generation;

    public VulkanShaderEntry Get(ShaderHandle handle) => _slots[(int)handle.Id]!.Value;

    public void Destroy(ShaderHandle handle)
    {
        if (!IsValid(handle)) return;
        _slots[(int)handle.Id] = null;
        _generations[(int)handle.Id]++;
        _freeSlots.Enqueue(handle.Id);
    }
}