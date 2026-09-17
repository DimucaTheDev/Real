using System;
using System.Collections.Generic;
using System.Numerics;
using Real.Graphics.Rhi;
using Real.Graphics.Rhi.Handles;
using Real.Graphics.Vulkan.Resources;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan;

/// <summary>
/// Thin recorder over a single VkCommandBuffer, active only between
/// vkCmdBeginRenderPass and vkCmdEndRenderPass for one RenderPassBuilder's
/// execution callback. Owned/created by VulkanRenderGraph, never directly
/// by user code.
/// </summary>
internal sealed class VulkanCommandList : ICommandList
{
    private readonly Vk _vk;
    private readonly Device _device;
    private CommandBuffer _cmd;
    private readonly VulkanPipelinePool _pipelines;
    private readonly VulkanBufferPool _buffers;
    private readonly VulkanTexturePool _textures;
    private readonly VulkanSamplerPool _samplers;
    private readonly VulkanDescriptorAllocator _descriptorAllocator;

    private readonly Dictionary<string, uint> _uniformOffsets = new(StringComparer.OrdinalIgnoreCase);
    private uint _nextPushConstantOffset = 0;

    private PipelineLayout _currentPipelineLayout;
    private Pipeline _currentPipeline;

    // Allocated once per BindPipeline() call (see VulkanDescriptorAllocator's
    // "we never free individual sets" design note) and written into by every
    // SetTexture/SetUniformBuffer call in between. Bound to the command buffer
    // lazily, right before the first draw call that follows - by then every
    // Set*() the caller intended for this draw has already landed in it.
    private DescriptorSet _currentDescriptorSet;
    private bool _descriptorSetBound;

    public VulkanCommandList(
        Vk vk,
        Device device,
        VulkanPipelinePool pipelines,
        VulkanBufferPool buffers,
        VulkanTexturePool textures,
        VulkanSamplerPool samplers,
        VulkanDescriptorAllocator descriptorAllocator)
    {
        _vk = vk;
        _device = device;
        _pipelines = pipelines;
        _buffers = buffers;
        _textures = textures;
        _samplers = samplers;
        _descriptorAllocator = descriptorAllocator;
    }

    public void Initialize(CommandBuffer cmd)
    {
        _cmd = cmd;
        _uniformOffsets.Clear();
        _nextPushConstantOffset = 0;
        _currentPipelineLayout = default;
        _currentPipeline = default;
        _currentDescriptorSet = default;
        _descriptorSetBound = false;
    }

    public void BindPipeline(PipelineHandle pipeline)
    {
        var entry = _pipelines.Get(pipeline);
        _currentPipelineLayout = entry.Layout;
        _currentPipeline = entry.Handle;

        _currentDescriptorSet = _descriptorAllocator.Allocate(entry.DescriptorSetLayout);
        _descriptorSetBound = false;

        _vk.CmdBindPipeline(_cmd, PipelineBindPoint.Graphics, entry.Handle);
    }

    public unsafe void BindVertexBuffer(BufferHandle buffer, uint slot = 0, ulong offset = 0)
    {
        var entry = _buffers.Get(buffer);
        var bufferHandle = entry.Handle;
        _vk.CmdBindVertexBuffers(_cmd, slot, 1, in bufferHandle, in offset);
    }

    public void BindIndexBuffer(BufferHandle buffer, ulong offset = 0)
    {
        var entry = _buffers.Get(buffer);
        // 32-bit indices assumed throughout the RHI - there's no IndexFormat
        // on the abstraction side to pick Uint16 vs Uint32 per call.
        _vk.CmdBindIndexBuffer(_cmd, entry.Handle, offset, IndexType.Uint32);
    }

    public unsafe void SetTexture(uint slot, TextureHandle texture, SamplerHandle sampler)
    {
        var textureEntry = _textures.Get(texture);
        var samplerHandle = _samplers.Get(sampler);

        var imageInfo = new DescriptorImageInfo
        {
            Sampler = samplerHandle,
            ImageView = textureEntry.View,
            // Matches the layout VulkanBarrierBuilder.TransitionForRead puts the
            // texture into - if this texture wasn't read via Reads() in the
            // render graph for this pass, this layout assumption is wrong.
            ImageLayout = ImageLayout.ShaderReadOnlyOptimal
        };

        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _currentDescriptorSet,
            DstBinding = slot,
            DstArrayElement = 0,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            PImageInfo = &imageInfo
        };

        _vk.UpdateDescriptorSets(_device, 1, in write, 0, null);
    }

    public unsafe void SetUniformBuffer(uint slot, BufferHandle buffer)
    {
        var entry = _buffers.Get(buffer);

        var bufferInfo = new DescriptorBufferInfo
        {
            Buffer = entry.Handle,
            Offset = 0,
            Range = entry.Size
        };

        var write = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = _currentDescriptorSet,
            DstBinding = slot,
            DstArrayElement = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            PBufferInfo = &bufferInfo
        };

        _vk.UpdateDescriptorSets(_device, 1, in write, 0, null);
    }

    public void SetViewport(float x, float y, float width, float height)
    {
        float w = width > 0f ? width : 1f;
        float h = height > 0f ? height : 1f;
        var viewport = new Viewport(x, y, w, h, 0f, 1f);
        _vk.CmdSetViewport(_cmd, 0, 1, in viewport);
    }

    public void SetScissor(int x, int y, uint width, uint height)
    {
        var scissor = new Rect2D(new Offset2D(x, y), new Extent2D(Math.Max(1u, width), Math.Max(1u, height)));
        _vk.CmdSetScissor(_cmd, 0, 1, in scissor);
    }

    public unsafe void PushConstants(ReadOnlySpan<byte> data, uint offset = 0)
    {
        fixed (byte* p = data)
        {
            _vk.CmdPushConstants(_cmd, _currentPipelineLayout,
                ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, offset, (uint)data.Length, p);
        }
    }

    private uint GetOrCreateUniformOffset(string name, uint size, uint alignment)
    {
        if (_uniformOffsets.TryGetValue(name, out var offset))
            return offset;

        // Predefined layout shortcuts matching std140 / push constant layout
        if (name.Equals("uMVP", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("MVP", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("uModelViewProjection", StringComparison.OrdinalIgnoreCase))
        {
            offset = 0;
            _uniformOffsets[name] = offset;
            _nextPushConstantOffset = Math.Max(_nextPushConstantOffset, offset + size);
            return offset;
        }

        if (name.Equals("uTime", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Time", StringComparison.OrdinalIgnoreCase))
        {
            offset = 64;
            _uniformOffsets[name] = offset;
            _nextPushConstantOffset = Math.Max(_nextPushConstantOffset, offset + size);
            return offset;
        }

        if (name.Equals("uAspect", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Aspect", StringComparison.OrdinalIgnoreCase))
        {
            offset = 68;
            _uniformOffsets[name] = offset;
            _nextPushConstantOffset = Math.Max(_nextPushConstantOffset, offset + size);
            return offset;
        }

        if (name.Equals("uResolution", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Resolution", StringComparison.OrdinalIgnoreCase))
        {
            offset = 72;
            _uniformOffsets[name] = offset;
            _nextPushConstantOffset = Math.Max(_nextPushConstantOffset, offset + size);
            return offset;
        }

        // Auto-align
        uint rem = _nextPushConstantOffset % alignment;
        if (rem != 0) _nextPushConstantOffset += (alignment - rem);

        offset = _nextPushConstantOffset;
        _uniformOffsets[name] = offset;
        _nextPushConstantOffset += size;
        return offset;
    }

    public unsafe void SetUniform(string name, int value)
    {
        uint offset = GetOrCreateUniformOffset(name, 4, 4);
        if (offset + 4 <= 128)
        {
            _vk.CmdPushConstants(_cmd, _currentPipelineLayout,
                ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, offset, 4, &value);
        }
    }

    public unsafe void SetUniform(string name, uint value)
    {
        uint offset = GetOrCreateUniformOffset(name, 4, 4);
        if (offset + 4 <= 128)
        {
            _vk.CmdPushConstants(_cmd, _currentPipelineLayout,
                ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, offset, 4, &value);
        }
    }

    public unsafe void SetUniform(string name, float value)
    {
        uint offset = GetOrCreateUniformOffset(name, 4, 4);
        if (offset + 4 <= 128)
        {
            _vk.CmdPushConstants(_cmd, _currentPipelineLayout,
                ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, offset, 4, &value);
        }
    }

    public unsafe void SetUniform(string name, bool value)
    {
        int val = value ? 1 : 0;
        uint offset = GetOrCreateUniformOffset(name, 4, 4);
        if (offset + 4 <= 128)
        {
            _vk.CmdPushConstants(_cmd, _currentPipelineLayout,
                ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, offset, 4, &val);
        }
    }

    public unsafe void SetUniform(string name, Vector2 value)
    {
        uint offset = GetOrCreateUniformOffset(name, 8, 8);
        if (offset + 8 <= 128)
        {
            _vk.CmdPushConstants(_cmd, _currentPipelineLayout,
                ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, offset, 8, &value);
        }
    }

    public void SetUniform(string name, float x, float y) => SetUniform(name, new Vector2(x, y));

    public unsafe void SetUniform(string name, Vector3 value)
    {
        uint offset = GetOrCreateUniformOffset(name, 12, 16);
        if (offset + 12 <= 128)
        {
            _vk.CmdPushConstants(_cmd, _currentPipelineLayout,
                ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, offset, 12, &value);
        }
    }

    public void SetUniform(string name, float x, float y, float z) => SetUniform(name, new Vector3(x, y, z));

    public unsafe void SetUniform(string name, Vector4 value)
    {
        uint offset = GetOrCreateUniformOffset(name, 16, 16);
        if (offset + 16 <= 128)
        {
            _vk.CmdPushConstants(_cmd, _currentPipelineLayout,
                ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, offset, 16, &value);
        }
    }

    public void SetUniform(string name, float x, float y, float z, float w) => SetUniform(name, new Vector4(x, y, z, w));

    public unsafe void SetUniform(string name, in Matrix4x4 value, bool transpose = false)
    {
        uint offset = GetOrCreateUniformOffset(name, 64, 16);
        if (offset + 64 <= 128)
        {
            if (transpose)
            {
                var transposed = Matrix4x4.Transpose(value);
                _vk.CmdPushConstants(_cmd, _currentPipelineLayout,
                    ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, offset, 64, &transposed);
            }
            else
            {
                fixed (Matrix4x4* p = &value)
                {
                    _vk.CmdPushConstants(_cmd, _currentPipelineLayout,
                        ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, offset, 64, p);
                }
            }
        }
    }

    public unsafe void SetUniform(string name, ReadOnlySpan<float> values)
    {
        uint size = (uint)(values.Length * sizeof(float));
        uint offset = GetOrCreateUniformOffset(name, size, 4);
        if (offset + size <= 128)
        {
            fixed (float* p = values)
            {
                _vk.CmdPushConstants(_cmd, _currentPipelineLayout,
                    ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, offset, size, p);
            }
        }
    }

    public unsafe void SetUniform(string name, ReadOnlySpan<int> values)
    {
        uint size = (uint)(values.Length * sizeof(int));
        uint offset = GetOrCreateUniformOffset(name, size, 4);
        if (offset + size <= 128)
        {
            fixed (int* p = values)
            {
                _vk.CmdPushConstants(_cmd, _currentPipelineLayout,
                    ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit, offset, size, p);
            }
        }
    }

    public void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0)
    {
        BindCurrentDescriptorSetIfNeeded();
        _vk.CmdDraw(_cmd, vertexCount, instanceCount, firstVertex, 0);
    }

    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0)
    {
        BindCurrentDescriptorSetIfNeeded();
        _vk.CmdDrawIndexed(_cmd, indexCount, instanceCount, firstIndex, 0, 0);
    }

    private unsafe void BindCurrentDescriptorSetIfNeeded()
    {
        if (_descriptorSetBound) return;

        var set = _currentDescriptorSet;
        if (set.Handle != 0)
        {
            _vk.CmdBindDescriptorSets(_cmd, PipelineBindPoint.Graphics, _currentPipelineLayout, 0, 1, in set, 0, null);
        }
        _descriptorSetBound = true;
    }
}