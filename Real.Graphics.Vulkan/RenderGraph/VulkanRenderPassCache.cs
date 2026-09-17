using System;
using System.Collections.Generic;
using Real.Graphics.Rhi;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Vulkan.Resources;
using Real.Graphics.Vulkan.Translation;
using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.RenderGraph;

/// <summary>
/// Caches VkRenderPass (+ matching VkFramebuffer) objects keyed by the set of
/// attachment formats a pipeline/pass uses. Vulkan render passes are cheap to
/// reuse across pipelines/passes that share the same attachment layout, so we
/// avoid recreating them every frame.
/// </summary>
internal sealed class VulkanRenderPassCache : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;

    internal readonly struct PassKey : IEquatable<PassKey>
    {
        public readonly TextureFormat C0;
        public readonly TextureFormat C1;
        public readonly TextureFormat C2;
        public readonly TextureFormat C3;
        public readonly int ColorCount;
        public readonly TextureFormat Depth;

        public PassKey(RenderPassBuilder pass, VulkanTexturePool textures)
        {
            ColorCount = pass.ColorWrites.Count;
            C0 = ColorCount > 0 ? textures.Get(pass.ColorWrites[0]).Descriptor.Format : (TextureFormat)0;
            C1 = ColorCount > 1 ? textures.Get(pass.ColorWrites[1]).Descriptor.Format : (TextureFormat)0;
            C2 = ColorCount > 2 ? textures.Get(pass.ColorWrites[2]).Descriptor.Format : (TextureFormat)0;
            C3 = ColorCount > 3 ? textures.Get(pass.ColorWrites[3]).Descriptor.Format : (TextureFormat)0;
            Depth = pass.DepthWrite is { } depth ? textures.Get(depth).Descriptor.Format : (TextureFormat)0;
        }

        public PassKey(TextureFormat[] colors, TextureFormat? depth)
        {
            ColorCount = colors.Length;
            C0 = ColorCount > 0 ? colors[0] : (TextureFormat)0;
            C1 = ColorCount > 1 ? colors[1] : (TextureFormat)0;
            C2 = ColorCount > 2 ? colors[2] : (TextureFormat)0;
            C3 = ColorCount > 3 ? colors[3] : (TextureFormat)0;
            Depth = depth ?? (TextureFormat)0;
        }

        public bool Equals(PassKey other) =>
            ColorCount == other.ColorCount &&
            C0 == other.C0 && C1 == other.C1 && C2 == other.C2 && C3 == other.C3 &&
            Depth == other.Depth;

        public override bool Equals(object? obj) => obj is PassKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ColorCount, C0, C1, C2, C3, Depth);
    }

    private readonly Dictionary<PassKey, RenderPass> _renderPasses = new();
    private readonly Dictionary<(RenderPass, ulong FramebufferKey), Framebuffer> _framebuffers = new();

    public VulkanRenderPassCache(Vk vk, Device device)
    {
        _vk = vk;
        _device = device;
    }

    public void Dispose() => DestroyAll();

    public unsafe RenderPass GetOrCreate(TextureFormat[] colorFormats, TextureFormat? depthFormat)
    {
        var key = new PassKey(colorFormats, depthFormat);
        if (_renderPasses.TryGetValue(key, out var existing))
            return existing;

        var colorCount = colorFormats.Length;
        bool hasDepth = depthFormat is not null;
        var attachmentCount = colorCount + (hasDepth ? 1 : 0);
        
        var attachments = new AttachmentDescription[attachmentCount];
        var colorRefs = new AttachmentReference[colorCount];

        for (int i = 0; i < colorCount; i++)
        {
            var format = colorFormats[i];
            attachments[i] = new AttachmentDescription
            {
                Format = VkFormatMap.ToVkFormat(format),
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
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
        if (hasDepth)
        {
            int depthIndex = colorCount;
            var format = depthFormat!.Value;
            attachments[depthIndex] = new AttachmentDescription
            {
                Format = VkFormatMap.ToVkFormat(format),
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

    public unsafe RenderPass GetOrCreate(RenderPassBuilder pass, VulkanTexturePool textures)
    {
        var key = new PassKey(pass, textures);
        if (_renderPasses.TryGetValue(key, out var existing))
            return existing;

        var colorCount = pass.ColorWrites.Count;
        bool hasDepth = pass.DepthWrite is not null;
        var attachmentCount = colorCount + (hasDepth ? 1 : 0);
        
        var attachments = new AttachmentDescription[attachmentCount];
        var colorRefs = new AttachmentReference[colorCount];

        for (int i = 0; i < colorCount; i++)
        {
            var format = textures.Get(pass.ColorWrites[i]).Descriptor.Format;
            attachments[i] = new AttachmentDescription
            {
                Format = VkFormatMap.ToVkFormat(format),
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
        if (hasDepth)
        {
            int depthIndex = colorCount;
            var format = textures.Get(pass.DepthWrite!.Value).Descriptor.Format;
            attachments[depthIndex] = new AttachmentDescription
            {
                Format = VkFormatMap.ToVkFormat(format),
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

    public unsafe Framebuffer GetOrCreateFramebuffer(RenderPass renderPass, ReadOnlySpan<ImageView> attachments, uint width, uint height)
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
    private static ulong ComputeAttachmentsKey(ReadOnlySpan<ImageView> attachments, uint width, uint height)
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

    public unsafe void ClearFramebuffers()
    {
        foreach (var framebuffer in _framebuffers.Values)
            _vk.DestroyFramebuffer(_device, framebuffer, null);
        _framebuffers.Clear();
    }

    public unsafe void DestroyAll()
    {
        ClearFramebuffers();

        foreach (var renderPass in _renderPasses.Values)
            _vk.DestroyRenderPass(_device, renderPass, null);
        _renderPasses.Clear();
    }
}