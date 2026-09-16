using System;
using System.Linq;
using System.Numerics;
using Real.Graphics.Rhi;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Real.Graphics.Vulkan.Core;
using Real.Graphics.Vulkan.RenderGraph;
using Real.Graphics.Vulkan.Resources;
using Real.Graphics.Vulkan.Sync;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Real.Graphics.Vulkan;

internal sealed class VulkanSwapchain : ISwapchain
{
    private readonly Vk _vk;
    private readonly VulkanLogicalDevice _device;
    private readonly KhrSwapchain _khrSwapchain;
    private readonly KhrSurface _khrSurface;
    private readonly SurfaceKHR _surface;
    private readonly VulkanTexturePool _textures;

    private SwapchainKHR _swapchain;
    private ImageView[] _imageViews = Array.Empty<ImageView>();
    private TextureHandle[] _backbufferHandles = Array.Empty<TextureHandle>();
    private Semaphore[] _renderFinishedSemaphores = Array.Empty<Semaphore>();
    private FrameSyncContext[] _frameSync = Array.Empty<FrameSyncContext>();
    private CommandPool _commandPool;

    private int _currentFrame;
    private uint _currentImageIndex;
    private uint _requestedWidth;
    private uint _requestedHeight;

    public TextureFormat Format { get; private set; }
    public uint Width { get; private set; }
    public uint Height { get; private set; }

    public int CurrentFrameIndex => _currentFrame;
    internal FrameSyncContext CurrentSync => _frameSync[_currentFrame];
    internal Semaphore CurrentRenderFinishedSemaphore =>
        _renderFinishedSemaphores.Length > _currentImageIndex ? _renderFinishedSemaphores[_currentImageIndex] : default;
    internal bool IsBackbuffer(TextureHandle handle) => _backbufferHandles.Contains(handle);

    private readonly VulkanRenderPassCache _renderPassCache;
    private readonly VulkanDescriptorAllocator _descriptorAllocator;

    public unsafe VulkanSwapchain(
        Vk vk,
        VulkanLogicalDevice device,
        KhrSwapchain khrSwapchain,
        KhrSurface khrSurface,
        SurfaceKHR surface,
        VulkanTexturePool textures,
        VulkanRenderPassCache renderPassCache,
        VulkanDescriptorAllocator descriptorAllocator,
        uint width,
        uint height)
    {
        _vk = vk;
        _device = device;
        _khrSwapchain = khrSwapchain;
        _khrSurface = khrSurface;
        _surface = surface;
        _textures = textures;
        _renderPassCache = renderPassCache;
        _descriptorAllocator = descriptorAllocator;

        _requestedWidth = width > 0 ? width : 800;
        _requestedHeight = height > 0 ? height : 600;

        var poolInfo = new CommandPoolCreateInfo
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
            QueueFamilyIndex = _device.Queues.GraphicsFamilyIndex
        };

        var poolResult = _vk.CreateCommandPool(_device.Handle, in poolInfo, null, out _commandPool);
        if (poolResult != Result.Success)
            throw new InvalidOperationException($"vkCreateCommandPool failed: {poolResult}");

        try
        {
            CreateSwapchain(_requestedWidth, _requestedHeight, default);
            CreateSyncObjects();
        }
        catch
        {
            _vk.DestroyCommandPool(_device.Handle, _commandPool, null);
            throw;
        }
    }

    private unsafe void CreateSwapchain(uint width, uint height, SwapchainKHR oldSwapchain)
    {
        _khrSurface.GetPhysicalDeviceSurfaceCapabilities(_device.PhysicalDevice, _surface, out var capabilities);

        if (capabilities.MaxImageExtent.Width == 0 || capabilities.MaxImageExtent.Height == 0)
        {
            return;
        }

        Extent2D extent;
        if (capabilities.CurrentExtent.Width != uint.MaxValue &&
            capabilities.CurrentExtent.Width > 0 &&
            capabilities.CurrentExtent.Height > 0)
        {
            extent = capabilities.CurrentExtent;
        }
        else
        {
            uint w = width > 0 ? width : _requestedWidth;
            if (w == 0) w = 800;
            uint h = height > 0 ? height : _requestedHeight;
            if (h == 0) h = 600;

            uint minW = capabilities.MinImageExtent.Width;
            uint minH = capabilities.MinImageExtent.Height;
            uint maxW = capabilities.MaxImageExtent.Width > 0 ? capabilities.MaxImageExtent.Width : uint.MaxValue;
            uint maxH = capabilities.MaxImageExtent.Height > 0 ? capabilities.MaxImageExtent.Height : uint.MaxValue;

            extent = new Extent2D(
                Math.Clamp(w, minW, maxW),
                Math.Clamp(h, minH, maxH));
        }

        if (extent.Width == 0 || extent.Height == 0)
        {
            return;
        }

        Width = extent.Width;
        Height = extent.Height;

        uint formatCount = 0;
        _khrSurface.GetPhysicalDeviceSurfaceFormats(_device.PhysicalDevice, _surface, ref formatCount, null);
        SurfaceFormatKHR chosenFormat;
        if (formatCount > 0)
        {
            var surfaceFormats = new SurfaceFormatKHR[formatCount];
            fixed (SurfaceFormatKHR* pFormats = surfaceFormats)
                _khrSurface.GetPhysicalDeviceSurfaceFormats(_device.PhysicalDevice, _surface, ref formatCount, pFormats);

            chosenFormat = surfaceFormats[0];
            foreach (var sf in surfaceFormats)
            {
                if (sf.Format is Silk.NET.Vulkan.Format.B8G8R8A8Srgb or Silk.NET.Vulkan.Format.R8G8B8A8Srgb &&
                    sf.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
                {
                    chosenFormat = sf;
                    break;
                }
            }
        }
        else
        {
            chosenFormat = new SurfaceFormatKHR
            {
                Format = Silk.NET.Vulkan.Format.B8G8R8A8Srgb,
                ColorSpace = ColorSpaceKHR.SpaceSrgbNonlinearKhr
            };
        }

        Format = chosenFormat.Format switch
        {
            Silk.NET.Vulkan.Format.B8G8R8A8Srgb => TextureFormat.Bgra8Srgb,
            Silk.NET.Vulkan.Format.B8G8R8A8Unorm => TextureFormat.Bgra8Unorm,
            Silk.NET.Vulkan.Format.R8G8B8A8Srgb => TextureFormat.Rgba8Srgb,
            Silk.NET.Vulkan.Format.R8G8B8A8Unorm => TextureFormat.Rgba8Unorm,
            _ => TextureFormat.Bgra8Srgb
        };

        uint presentModeCount = 0;
        _khrSurface.GetPhysicalDeviceSurfacePresentModes(_device.PhysicalDevice, _surface, ref presentModeCount, null);
        var chosenPresentMode = PresentModeKHR.FifoKhr;
        if (presentModeCount > 0)
        {
            var presentModes = new PresentModeKHR[presentModeCount];
            fixed (PresentModeKHR* pModes = presentModes)
                _khrSurface.GetPhysicalDeviceSurfacePresentModes(_device.PhysicalDevice, _surface, ref presentModeCount, pModes);

            foreach (var mode in presentModes)
            {
                if (mode == PresentModeKHR.MailboxKhr)
                {
                    chosenPresentMode = mode;
                    break;
                }
            }
        }

        uint minImageCount = capabilities.MinImageCount > 0 ? capabilities.MinImageCount + 1 : 2;
        if (capabilities.MaxImageCount > 0 && minImageCount > capabilities.MaxImageCount)
            minImageCount = capabilities.MaxImageCount;

        var sharingMode = SharingMode.Exclusive;
        uint queueFamilyIndicesCount = 0;
        uint[] queueFamilyIndices = [_device.Queues.GraphicsFamilyIndex, _device.Queues.PresentFamilyIndex];
        if (_device.Queues.GraphicsFamilyIndex != _device.Queues.PresentFamilyIndex)
        {
            sharingMode = SharingMode.Concurrent;
            queueFamilyIndicesCount = 2;
        }

        var preTransform = capabilities.CurrentTransform != 0
            ? capabilities.CurrentTransform
            : ((capabilities.SupportedTransforms & SurfaceTransformFlagsKHR.IdentityBitKhr) != 0
                ? SurfaceTransformFlagsKHR.IdentityBitKhr
                : (capabilities.SupportedTransforms != 0
                    ? (SurfaceTransformFlagsKHR)(1u << BitOperations.TrailingZeroCount((uint)capabilities.SupportedTransforms))
                    : SurfaceTransformFlagsKHR.IdentityBitKhr));

        var compositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr;
        if ((capabilities.SupportedCompositeAlpha & CompositeAlphaFlagsKHR.OpaqueBitKhr) == 0)
        {
            if ((capabilities.SupportedCompositeAlpha & CompositeAlphaFlagsKHR.InheritBitKhr) != 0)
                compositeAlpha = CompositeAlphaFlagsKHR.InheritBitKhr;
            else if ((capabilities.SupportedCompositeAlpha & CompositeAlphaFlagsKHR.PreMultipliedBitKhr) != 0)
                compositeAlpha = CompositeAlphaFlagsKHR.PreMultipliedBitKhr;
            else if ((capabilities.SupportedCompositeAlpha & CompositeAlphaFlagsKHR.PostMultipliedBitKhr) != 0)
                compositeAlpha = CompositeAlphaFlagsKHR.PostMultipliedBitKhr;
        }

        fixed (uint* pQueueIndices = queueFamilyIndices)
        {
            var createInfo = new SwapchainCreateInfoKHR
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = _surface,
                MinImageCount = minImageCount,
                ImageFormat = chosenFormat.Format,
                ImageColorSpace = chosenFormat.ColorSpace,
                ImageExtent = extent,
                ImageArrayLayers = 1,
                ImageUsage = ImageUsageFlags.ColorAttachmentBit | ImageUsageFlags.TransferDstBit,
                ImageSharingMode = sharingMode,
                QueueFamilyIndexCount = queueFamilyIndicesCount,
                PQueueFamilyIndices = queueFamilyIndicesCount > 0 ? pQueueIndices : null,
                PreTransform = preTransform,
                CompositeAlpha = compositeAlpha,
                PresentMode = chosenPresentMode,
                Clipped = true,
                OldSwapchain = oldSwapchain
            };

            var result = _khrSwapchain.CreateSwapchain(_device.Handle, in createInfo, null, out _swapchain);
            if (result != Result.Success)
                throw new InvalidOperationException($"vkCreateSwapchainKHR failed: {result}");
        }

        uint imageCount = 0;
        _khrSwapchain.GetSwapchainImages(_device.Handle, _swapchain, ref imageCount, null);
        var images = new Image[imageCount];
        fixed (Image* pImages = images)
            _khrSwapchain.GetSwapchainImages(_device.Handle, _swapchain, ref imageCount, pImages);

        _backbufferHandles = new TextureHandle[imageCount];
        _imageViews = new ImageView[imageCount];
        _renderFinishedSemaphores = new Semaphore[imageCount];

        var descriptor = new TextureDescriptor(Width, Height, Format, TextureUsage.RenderTarget);

        for (int i = 0; i < imageCount; i++)
        {
            var semInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
            var semRes = _vk.CreateSemaphore(_device.Handle, in semInfo, null, out _renderFinishedSemaphores[i]);
            if (semRes != Result.Success)
                throw new InvalidOperationException($"vkCreateSemaphore failed: {semRes}");

            var viewInfo = new ImageViewCreateInfo
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = images[i],
                ViewType = ImageViewType.Type2D,
                Format = chosenFormat.Format,
                Components = new ComponentMapping(
                    ComponentSwizzle.Identity,
                    ComponentSwizzle.Identity,
                    ComponentSwizzle.Identity,
                    ComponentSwizzle.Identity),
                SubresourceRange = new ImageSubresourceRange(
                    ImageAspectFlags.ColorBit,
                    0, 1, 0, 1)
            };

            var res = _vk.CreateImageView(_device.Handle, in viewInfo, null, out _imageViews[i]);
            if (res != Result.Success)
                throw new InvalidOperationException($"vkCreateImageView failed: {res}");

            _backbufferHandles[i] = _textures.Wrap(images[i], _imageViews[i], descriptor, ImageLayout.Undefined);
        }
    }

    private unsafe void CreateSyncObjects()
    {
        _frameSync = new FrameSyncContext[FrameSyncContext.FramesInFlight];
        for (int i = 0; i < FrameSyncContext.FramesInFlight; i++)
        {
            var semInfo = new SemaphoreCreateInfo { SType = StructureType.SemaphoreCreateInfo };
            var fenceInfo = new FenceCreateInfo
            {
                SType = StructureType.FenceCreateInfo,
                Flags = FenceCreateFlags.SignaledBit
            };

            _vk.CreateSemaphore(_device.Handle, in semInfo, null, out var imageAvailable);
            _vk.CreateFence(_device.Handle, in fenceInfo, null, out var fence);

            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _commandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };

            _vk.AllocateCommandBuffers(_device.Handle, in allocInfo, out var cmdBuffer);

            _frameSync[i] = new FrameSyncContext
            {
                ImageAvailable = imageAvailable,
                InFlightFence = fence,
                CommandBuffer = cmdBuffer
            };
        }
    }

    public unsafe TextureHandle AcquireNextImage()
    {
        if (_swapchain.Handle == 0 || _backbufferHandles.Length == 0)
        {
            Resize(_requestedWidth, _requestedHeight);
            if (_swapchain.Handle == 0 || _backbufferHandles.Length == 0)
                return default;
        }

        var sync = _frameSync[_currentFrame];
        var fence = sync.InFlightFence;

        _vk.WaitForFences(_device.Handle, 1, in fence, true, ulong.MaxValue);

        var result = _khrSwapchain.AcquireNextImage(
            _device.Handle,
            _swapchain,
            ulong.MaxValue,
            sync.ImageAvailable,
            default,
            ref _currentImageIndex);

        if (result == Result.ErrorOutOfDateKhr)
        {
            Resize(_requestedWidth, _requestedHeight);
            if (_swapchain.Handle == 0 || _backbufferHandles.Length == 0)
                return default;

            result = _khrSwapchain.AcquireNextImage(
                _device.Handle,
                _swapchain,
                ulong.MaxValue,
                sync.ImageAvailable,
                default,
                ref _currentImageIndex);

            if (result is not (Result.Success or Result.SuboptimalKhr))
                return default;
        }
        else if (result is not (Result.Success or Result.SuboptimalKhr))
        {
            throw new InvalidOperationException($"vkAcquireNextImageKHR failed: {result}");
        }

        if (_backbufferHandles.Length == 0 || _currentImageIndex >= _backbufferHandles.Length)
            return default;

        _descriptorAllocator.ResetFrame(_currentFrame);
        _descriptorAllocator.SetCurrentFrame(_currentFrame);

        return _backbufferHandles[_currentImageIndex];
    }

    public unsafe void Present()
    {
        if (_swapchain.Handle == 0 || _renderFinishedSemaphores.Length <= _currentImageIndex)
            return;

        var waitSemaphore = _renderFinishedSemaphores[_currentImageIndex];
        var swapchain = _swapchain;
        var imageIndex = _currentImageIndex;

        var presentInfo = new PresentInfoKHR
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = waitSemaphore.Handle != 0 ? 1u : 0u,
            PWaitSemaphores = waitSemaphore.Handle != 0 ? &waitSemaphore : null,
            SwapchainCount = 1,
            PSwapchains = &swapchain,
            PImageIndices = &imageIndex
        };

        var result = _khrSwapchain.QueuePresent(_device.Queues.PresentQueue, in presentInfo);
        if (result is Result.ErrorOutOfDateKhr or Result.SuboptimalKhr)
        {
            Resize(_requestedWidth, _requestedHeight);
        }
        else if (result != Result.Success)
        {
            throw new InvalidOperationException($"vkQueuePresentKHR failed: {result}");
        }

        _currentFrame = (_currentFrame + 1) % FrameSyncContext.FramesInFlight;
    }

    public unsafe void Resize(uint width, uint height)
    {
        if (width > 0) _requestedWidth = width;
        if (height > 0) _requestedHeight = height;

        _khrSurface.GetPhysicalDeviceSurfaceCapabilities(_device.PhysicalDevice, _surface, out var capabilities);
        if (capabilities.MaxImageExtent.Width == 0 || capabilities.MaxImageExtent.Height == 0)
        {
            return;
        }
        if (capabilities.CurrentExtent.Width != uint.MaxValue &&
            (capabilities.CurrentExtent.Width == 0 || capabilities.CurrentExtent.Height == 0))
        {
            return;
        }

        _vk.DeviceWaitIdle(_device.Handle);

        var oldSwapchain = _swapchain;
        DestroySwapchainImageViews();
        _renderPassCache.ClearFramebuffers();
        CreateSwapchain(_requestedWidth, _requestedHeight, oldSwapchain);

        if (oldSwapchain.Handle != 0)
        {
            _khrSwapchain.DestroySwapchain(_device.Handle, oldSwapchain, null);
        }
    }

    private unsafe void DestroySwapchainImageViews()
    {
        for (int i = 0; i < _renderFinishedSemaphores.Length; i++)
        {
            if (_renderFinishedSemaphores[i].Handle != 0)
            {
                _vk.DestroySemaphore(_device.Handle, _renderFinishedSemaphores[i], null);
            }
        }
        _renderFinishedSemaphores = Array.Empty<Semaphore>();

        for (int i = 0; i < _backbufferHandles.Length; i++)
        {
            if (_textures.IsValid(_backbufferHandles[i]))
            {
                _textures.Destroy(_backbufferHandles[i]);
            }
        }
        _backbufferHandles = Array.Empty<TextureHandle>();
        _imageViews = Array.Empty<ImageView>();
    }

    public unsafe void Dispose()
    {
        _vk.DeviceWaitIdle(_device.Handle);

        DestroySwapchainImageViews();

        if (_swapchain.Handle != 0)
        {
            _khrSwapchain.DestroySwapchain(_device.Handle, _swapchain, null);
            _swapchain = default;
        }

        foreach (var sync in _frameSync)
        {
            if (sync.ImageAvailable.Handle != 0) _vk.DestroySemaphore(_device.Handle, sync.ImageAvailable, null);
            if (sync.InFlightFence.Handle != 0) _vk.DestroyFence(_device.Handle, sync.InFlightFence, null);
        }
        _frameSync = Array.Empty<FrameSyncContext>();

        if (_commandPool.Handle != 0)
        {
            _vk.DestroyCommandPool(_device.Handle, _commandPool, null);
            _commandPool = default;
        }
    }
}
