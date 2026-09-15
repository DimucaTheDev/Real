using System;
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
    private readonly CommandBuffer _cmd;
    private readonly VulkanPipelinePool _pipelines;
    private readonly VulkanBufferPool _buffers;
    private readonly VulkanTexturePool _textures;
    private readonly VulkanSamplerPool _samplers;
    private readonly VulkanDescriptorAllocator _descriptorAllocator;

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
        CommandBuffer cmd,
        VulkanPipelinePool pipelines,
        VulkanBufferPool buffers,
        VulkanTexturePool textures,
        VulkanSamplerPool samplers,
        VulkanDescriptorAllocator descriptorAllocator)
    {
        _vk = vk;
        _cmd = cmd;
        _pipelines = pipelines;
        _buffers = buffers;
        _textures = textures;
        _samplers = samplers;
        _descriptorAllocator = descriptorAllocator;
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
        _vk.CmdBindVertexBuffers(_cmd, slot, 1, in entry.Handle, in offset);
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

        _vk.UpdateDescriptorSets(_vk.CurrentDevice!.Value, 1, in write, 0, null);
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

        _vk.UpdateDescriptorSets(_vk.CurrentDevice!.Value, 1, in write, 0, null);
    }

    public void SetViewport(float x, float y, float width, float height)
    {
        var viewport = new Viewport(x, y, width, height, 0f, 1f);
        _vk.CmdSetViewport(_cmd, 0, 1, in viewport);
    }

    public void SetScissor(int x, int y, uint width, uint height)
    {
        var scissor = new Rect2D(new Offset2D(x, y), new Extent2D(width, height));
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
        _vk.CmdBindDescriptorSets(_cmd, PipelineBindPoint.Graphics, _currentPipelineLayout, 0, 1, in set, 0, null);
        _descriptorSetBound = true;
    }
}