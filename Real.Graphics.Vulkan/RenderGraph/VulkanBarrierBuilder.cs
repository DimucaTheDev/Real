using Real.Graphics.Rhi.Handles;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.RenderGraph;

/// <summary>
/// Translates a pass's declared Reads()/Writes() (from RenderPassBuilder) into
/// concrete vkCmdPipelineBarrier / VkImageMemoryBarrier calls, by diffing the
/// resource's previous state (from VulkanResourceTracker) against what this
/// pass requires. This is the piece that makes explicit barrier calls
/// unnecessary in user code.
/// </summary>
internal sealed class VulkanBarrierBuilder
{
    private readonly Vk _vk;
    private readonly VulkanResourceTracker _tracker;

    public VulkanBarrierBuilder(Vk vk, VulkanResourceTracker tracker)
    {
        _vk = vk;
        _tracker = tracker;
    }

    public unsafe void TransitionForRead(CommandBuffer cmd, TextureHandle texture, Image image)
    {
        var (oldLayout, oldAccess, oldStage) = _tracker.GetState(texture);
        const ImageLayout newLayout = ImageLayout.ShaderReadOnlyOptimal;

        if (oldLayout == newLayout) return;

        EmitBarrier(
            cmd, image,
            oldLayout, newLayout,
            oldStage, PipelineStageFlags.FragmentShaderBit,
            MapAccess(oldAccess), AccessFlags.ShaderReadBit,
            ImageAspectFlags.ColorBit);

        _tracker.SetState(texture, newLayout, ResourceAccess.ShaderRead, PipelineStageFlags.FragmentShaderBit);
    }

    public unsafe void TransitionForColorWrite(CommandBuffer cmd, TextureHandle texture, Image image)
    {
        var (oldLayout, oldAccess, oldStage) = _tracker.GetState(texture);
        const ImageLayout newLayout = ImageLayout.ColorAttachmentOptimal;

        if (oldLayout == newLayout) return;

        EmitBarrier(
            cmd, image,
            oldLayout, newLayout,
            oldStage, PipelineStageFlags.ColorAttachmentOutputBit,
            MapAccess(oldAccess), AccessFlags.ColorAttachmentWriteBit,
            ImageAspectFlags.ColorBit);

        _tracker.SetState(texture, newLayout, ResourceAccess.ColorAttachmentWrite, PipelineStageFlags.ColorAttachmentOutputBit);
    }

    public unsafe void TransitionForDepthWrite(CommandBuffer cmd, TextureHandle texture, Image image)
    {
        var (oldLayout, oldAccess, oldStage) = _tracker.GetState(texture);
        const ImageLayout newLayout = ImageLayout.DepthStencilAttachmentOptimal;

        if (oldLayout == newLayout) return;

        EmitBarrier(
            cmd, image,
            oldLayout, newLayout,
            oldStage, PipelineStageFlags.EarlyFragmentTestsBit | PipelineStageFlags.LateFragmentTestsBit,
            MapAccess(oldAccess), AccessFlags.DepthStencilAttachmentReadBit | AccessFlags.DepthStencilAttachmentWriteBit,
            ImageAspectFlags.DepthBit | ImageAspectFlags.StencilBit);

        _tracker.SetState(texture, newLayout, ResourceAccess.DepthAttachmentWrite, PipelineStageFlags.EarlyFragmentTestsBit);
    }

    public unsafe void TransitionForPresent(CommandBuffer cmd, TextureHandle texture, Image image)
    {
        var (oldLayout, oldAccess, oldStage) = _tracker.GetState(texture);
        const ImageLayout newLayout = ImageLayout.PresentSrcKhr;

        if (oldLayout == newLayout) return;

        EmitBarrier(
            cmd, image,
            oldLayout, newLayout,
            oldStage, PipelineStageFlags.BottomOfPipeBit,
            MapAccess(oldAccess), AccessFlags.None,
            ImageAspectFlags.ColorBit);

        _tracker.SetState(texture, newLayout, ResourceAccess.PresentSrc, PipelineStageFlags.BottomOfPipeBit);
    }

    private unsafe void EmitBarrier(
        CommandBuffer cmd,
        Image image,
        ImageLayout oldLayout, ImageLayout newLayout,
        PipelineStageFlags srcStage, PipelineStageFlags dstStage,
        AccessFlags srcAccess, AccessFlags dstAccess,
        ImageAspectFlags aspect)
    {
        var barrier = new ImageMemoryBarrier
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            // Not doing a queue family ownership transfer here - graphics/present/
            // transfer queues share resources without explicit handoff in this RHI.
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = aspect,
                BaseMipLevel = 0,
                LevelCount = Vk.RemainingMipLevels,
                BaseArrayLayer = 0,
                LayerCount = Vk.RemainingArrayLayers
            },
            SrcAccessMask = srcAccess,
            DstAccessMask = dstAccess
        };

        _vk.CmdPipelineBarrier(
            cmd,
            srcStage, dstStage,
            DependencyFlags.None,
            0, null,
            0, null,
            1, &barrier);
    }

    /// <summary>
    /// Maps the coarse ResourceAccess the tracker stores back to the precise
    /// VkAccessFlags needed as srcAccessMask - i.e. "what kind of access to
    /// this resource must complete before the barrier lets anything past it".
    /// </summary>
    private static AccessFlags MapAccess(ResourceAccess access) => access switch
    {
        ResourceAccess.None => AccessFlags.None,
        ResourceAccess.ColorAttachmentWrite => AccessFlags.ColorAttachmentWriteBit,
        ResourceAccess.DepthAttachmentWrite => AccessFlags.DepthStencilAttachmentWriteBit,
        ResourceAccess.ShaderRead => AccessFlags.ShaderReadBit,
        ResourceAccess.TransferSrc => AccessFlags.TransferReadBit,
        ResourceAccess.TransferDst => AccessFlags.TransferWriteBit,
        ResourceAccess.PresentSrc => AccessFlags.None,
        _ => AccessFlags.None
    };
}