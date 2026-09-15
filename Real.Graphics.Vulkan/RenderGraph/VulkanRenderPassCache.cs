using System;
using System.Collections.Generic;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Vulkan.Translation;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.RenderGraph;

/// <summary>
/// Caches VkRenderPass (+ matching VkFramebuffer) objects keyed by the set of
/// attachment formats a pipeline/pass uses. Vulkan render passes are cheap to
/// reuse across pipelines/passes that share the same attachment layout, so we
/// avoid recreating them every frame.
/// </summary>
internal sealed class VulkanRenderPassCache
{
    private readonly Vk _vk;
    private readonly Device _device;

    /// <summary>
    /// TextureFormat[] doesn't get content equality "for free" from a record
    /// struct - arrays compare by reference. IEquatable is implemented
    /// explicitly below so two Keys with the same formats in the same order
    /// are treated as equal, otherwise every call would miss the cache.
    /// </summary>
    private readonly record struct Key(TextureFormat[] ColorFormats, TextureFormat? DepthFormat) : IEquatable<Key>
    {
        public bool Equals(Key other) =>
            ColorFormats.AsSpan().SequenceEqual(other.ColorFormats) && DepthFormat == other.DepthFormat;

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var format in ColorFormats)
                hash.Add(format);
            hash.Add(DepthFormat);
            return hash.ToHashCode();
        }
    }

    private readonly Dictionary<Key, RenderPass> _renderPasses = new();
    private readonly Dictionary<(RenderPass, ulong FramebufferKey), Framebuffer> _framebuffers = new();

    public VulkanRenderPassCache(Vk vk, Device device)
    {
        _vk = vk;
        _device = device;
    }

    public unsafe RenderPass GetOrCreate(TextureFormat[] colorFormats, TextureFormat? depthFormat)
    {
        var key = new Key(colorFormats, depthFormat);
        if (_renderPasses.TryGetValue(key, out var existing))
            return existing;

        var attachmentCount = colorFormats.Length + (depthFormat is not null ? 1 : 0);
        var attachments = new AttachmentDescription[attachmentCount];
        var colorRefs = new AttachmentReference[colorFormats.Length];

        for (int i = 0; i < colorFormats.Length; i++)
        {
            attachments[i] = new AttachmentDescription
            {
                Format = VkFormatMap.ToVkFormat(colorFormats[i]),
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                // VulkanBarrierBuilder already puts the image into ColorAttachmentOptimal
                // before BeginRenderPass - the render pass performs no transition itself.
                InitialLayout = ImageLayout.ColorAttachmentOptimal,
                FinalLayout = ImageLayout.ColorAttachmentOptimal
            };

            colorRefs[i] = new AttachmentReference
            {
                Attachment = (uint)i,
                Layout = ImageLayout.ColorAttachmentOptimal
            };
        }

        AttachmentReference depthRef = default;
        bool hasDepth = depthFormat is not null;
        if (hasDepth)
        {
            int depthIndex = colorFormats.Length;
            attachments[depthIndex] = new AttachmentDescription
            {
                Format = VkFormatMap.ToVkFormat(depthFormat!.Value),
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.DontCare,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.DepthStencilAttachmentOptimal,
                FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
            };

            depthRef = new AttachmentReference
            {
                Attachment = (uint)depthIndex,
                Layout = ImageLayout.DepthStencilAttachmentOptimal
            };
        }

        fixed (AttachmentReference* pColorRefs = colorRefs)
        {
            var subpass = new SubpassDescription
            {
                PipelineBindPoint = PipelineBindPoint.Graphics,
                ColorAttachmentCount = (uint)colorRefs.Length,
                PColorAttachments = pColorRefs,
                PDepthStencilAttachment = hasDepth ? &depthRef : null
            };

            fixed (AttachmentDescription* pAttachments = attachments)
            {
                // No VkSubpassDependency here: synchronization for these attachments
                // is handled entirely by VulkanBarrierBuilder outside the render pass.
                var renderPassInfo = new RenderPassCreateInfo
                {
                    SType = StructureType.RenderPassCreateInfo,
                    AttachmentCount = (uint)attachments.Length,
                    PAttachments = pAttachments,
                    SubpassCount = 1,
                    PSubpasses = &subpass,
                    DependencyCount = 0
                };

                var result = _vk.CreateRenderPass(_device, in renderPassInfo, null, out var renderPass);
                if (result != Result.Success)
                    throw new InvalidOperationException($"vkCreateRenderPass failed: {result}");

                _renderPasses[key] = renderPass;
                return renderPass;
            }
        }
    }

    public unsafe Framebuffer GetOrCreateFramebuffer(RenderPass renderPass, ImageView[] attachments, uint width, uint height)
    {
        ulong framebufferKey = ComputeAttachmentsKey(attachments, width, height);
        var cacheKey = (renderPass, framebufferKey);

        if (_framebuffers.TryGetValue(cacheKey, out var existing))
            return existing;

        fixed (ImageView* pAttachments = attachments)
        {
            var createInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = renderPass,
                AttachmentCount = (uint)attachments.Length,
                PAttachments = pAttachments,
                Width = width,
                Height = height,
                Layers = 1
            };

            var result = _vk.CreateFramebuffer(_device, in createInfo, null, out var framebuffer);
            if (result != Result.Success)
                throw new InvalidOperationException($"vkCreateFramebuffer failed: {result}");

            _framebuffers[cacheKey] = framebuffer;
            return framebuffer;
        }
    }

    /// <summary>
    /// FNV-1a over the attachment ImageView handles + target size. A resized
    /// swapchain produces new ImageViews (new handles) anyway, so size is
    /// technically redundant with the handles, but folding it in keeps the key
    /// correct even if this is ever called with reused/aliased views.
    /// </summary>
    private static ulong ComputeAttachmentsKey(ImageView[] attachments, uint width, uint height)
    {
        unchecked
        {
            ulong hash = 1469598103934665603; // FNV-1a offset basis
            foreach (var view in attachments)
            {
                hash ^= view.Handle;
                hash *= 1099511628211; // FNV-1a prime
            }
            hash ^= width;
            hash *= 1099511628211;
            hash ^= height;
            hash *= 1099511628211;
            return hash;
        }
    }

    public unsafe void DestroyAll()
    {
        foreach (var framebuffer in _framebuffers.Values)
            _vk.DestroyFramebuffer(_device, framebuffer, null);
        _framebuffers.Clear();

        foreach (var renderPass in _renderPasses.Values)
            _vk.DestroyRenderPass(_device, renderPass, null);
        _renderPasses.Clear();
    }
}