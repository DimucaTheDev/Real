using Real.Graphics.Rhi;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Real.Graphics.Vulkan.Core;
using Real.Graphics.Vulkan.Resources;
using Real.Graphics.Vulkan.Sync;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Real.Graphics.Vulkan;

internal sealed class VulkanSwapchain : ISwapchain
{
    private readonly Vk _vk;
    private readonly VulkanLogicalDevice _device;
    private readonly KhrSwapchain _khrSwapchain;
    private readonly SurfaceKHR _surface;
    private readonly VulkanTexturePool _textures;

    private SwapchainKHR _swapchain;
    private TextureHandle[] _backbufferHandles = Array.Empty<TextureHandle>();
    private FrameSyncContext[] _frameSync = Array.Empty<FrameSyncContext>();
    private int _currentFrame;
    private uint _currentImageIndex;

    public TextureFormat Format { get; private set; }
    public uint Width { get; private set; }
    public uint Height { get; private set; }

    public VulkanSwapchain(
        Vk vk,
        VulkanLogicalDevice device,
        KhrSwapchain khrSwapchain,
        SurfaceKHR surface,
        VulkanTexturePool textures,
        uint width,
        uint height)
    {
        _vk = vk;
        _device = device;
        _khrSwapchain = khrSwapchain;
        _surface = surface;
        _textures = textures;

        CreateSwapchain(width, height);
    }

    private void CreateSwapchain(uint width, uint height)
    {
        Width = width;
        Height = height;
        Format = TextureFormat.Rgba8Srgb;

        // TODO:
        // 1. query VkSurfaceCapabilitiesKHR / formats / present modes
        // 2. pick format (prefer SRGB), present mode (prefer Mailbox, fallback Fifo)
        // 3. vk.CreateSwapchainKHR via _khrSwapchain
        // 4. GetSwapchainImages, CreateImageView per image
        // 5. wrap each VkImage/VkImageView as a TextureHandle via _textures.Wrap(...)
        // 6. create FrameSyncContext[FrameSyncContext.FramesInFlight]
    }

    public TextureHandle AcquireNextImage()
    {
        var sync = _frameSync[_currentFrame];

        // TODO: vk.WaitForFences(sync.InFlightFence), vk.ResetFences,
        // _khrSwapchain.AcquireNextImage(..., sync.ImageAvailable, ..., out _currentImageIndex)
        // handle VK_ERROR_OUT_OF_DATE_KHR by calling Resize(Width, Height)

        return _backbufferHandles[_currentImageIndex];
    }

    public void Present()
    {
        var sync = _frameSync[_currentFrame];

        // TODO: submit the frame's command buffer waiting on sync.ImageAvailable,
        // signaling sync.RenderFinished and sync.InFlightFence, then
        // _khrSwapchain.QueuePresent waiting on sync.RenderFinished.
        // handle VK_SUBOPTIMAL_KHR / VK_ERROR_OUT_OF_DATE_KHR by calling Resize.

        _currentFrame = (_currentFrame + 1) % FrameSyncContext.FramesInFlight;
    }

    public void Resize(uint width, uint height)
    {
        // TODO: device.WaitIdle(); destroy old swapchain/image views (keep old
        // swapchain as `oldSwapchain` in create info for a smoother transition);
        // CreateSwapchain(width, height);
    }

    public void Dispose()
    {
        // TODO: destroy image views, _khrSwapchain.DestroySwapchain, fences/semaphores
    }
}
