using Real.Graphics.Rhi;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Real.Graphics.Vulkan.Core;
using Real.Graphics.Vulkan.RenderGraph;
using Real.Graphics.Vulkan.Resources;
using Real.Graphics.Vulkan.Surface;
using Real.Windowing;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace Real.Graphics.Vulkan;

/// <summary>
/// Vulkan implementation of IGraphicsDevice. Owns VkInstance/VkDevice lifetime
/// (via VulkanInstance/VulkanLogicalDevice) and the resource pools; translates
/// the abstract descriptors into VkCreateInfo structs via Translation/*.
/// </summary>
public sealed class VulkanDevice : IGraphicsDevice
{
    public string BackendName => "Vulkan";

    private readonly VulkanInstance _instance;
    private readonly SurfaceKHR _surface;
    private readonly VulkanLogicalDevice _logicalDevice;
    private readonly VulkanDebugMessenger? _debugMessenger;

    private readonly VulkanMemoryAllocator _memoryAllocator;
    private readonly VulkanBufferPool _bufferPool;
    private readonly VulkanTexturePool _texturePool;
    private readonly VulkanShaderCompiler _shaderCompiler;
    private readonly VulkanDescriptorAllocator _descriptorAllocator;
    private readonly VulkanRenderPassCache _renderPassCache;
    private readonly VulkanPipelinePool _pipelinePool;

    public VulkanDevice(IWindow window, bool enableValidation = false)
    {
        if (window.GraphicsApi != GraphicsApi.Vulkan)
            throw new InvalidOperationException(
                "VulkanDevice requires a window created with WindowSettings.GraphicsApi = GraphicsApi.Vulkan.");

        _instance = new VulkanInstance(appName: "RealEngine", enableValidation);

        _surface = SurfaceFactory.CreateForWindow(_instance.Vk, _instance.Handle, window.Handle);

        var physicalDevice = VulkanPhysicalDeviceSelector.Select(_instance, _surface);
        _logicalDevice = new VulkanLogicalDevice(_instance.Vk, _instance.Handle, physicalDevice, _surface);

        if (enableValidation)
        {
            // TODO: resolve ExtDebugUtils extension, construct _debugMessenger
        }

        _memoryAllocator =
            new VulkanMemoryAllocator(_instance.Vk, _logicalDevice.PhysicalDevice, _logicalDevice.Handle);
        _bufferPool = new VulkanBufferPool(_instance.Vk, _logicalDevice.Handle, _memoryAllocator);
        _texturePool = new VulkanTexturePool(_instance.Vk, _logicalDevice.Handle, _memoryAllocator);
        _shaderCompiler = new VulkanShaderCompiler(_instance.Vk, _logicalDevice.Handle);
        _descriptorAllocator = new VulkanDescriptorAllocator(_instance.Vk, _logicalDevice.Handle);
        _renderPassCache = new VulkanRenderPassCache(_instance.Vk, _logicalDevice.Handle);
        _pipelinePool = new VulkanPipelinePool(_instance.Vk, _logicalDevice.Handle, _renderPassCache);
    }

    public BufferHandle CreateBuffer(in BufferDescriptor descriptor, ReadOnlySpan<byte> initialData = default) =>
        _bufferPool.Create(descriptor, initialData);

    public void DestroyBuffer(BufferHandle handle) => _bufferPool.Destroy(handle);

    public TextureHandle CreateTexture(in TextureDescriptor descriptor, ReadOnlySpan<byte> initialData = default) =>
        _texturePool.Create(descriptor, initialData);

    public void DestroyTexture(TextureHandle handle) => _texturePool.Destroy(handle);

    public SamplerHandle CreateSampler(in SamplerDescriptor descriptor)
    {
        // TODO: vk.CreateSampler from descriptor, store in a small sampler pool
        // (omitted here - same slot-pool pattern as buffers/textures).
        return SamplerHandle.Invalid;
    }

    public void DestroySampler(SamplerHandle handle)
    {
        // TODO
    }

    public ShaderHandle CreateShader(ShaderStage stage, ReadOnlySpan<byte> bytecode, string entryPoint = "main")
    {
        var module = _shaderCompiler.CreateModule(bytecode);
        // TODO: store (module, stage, entryPoint) in a shader pool, return its handle
        return ShaderHandle.Invalid;
    }

    public void DestroyShader(ShaderHandle handle)
    {
        // TODO
    }

    public PipelineHandle CreatePipeline(in PipelineDescriptor descriptor) =>
        _pipelinePool.Create(descriptor);

    public void DestroyPipeline(PipelineHandle handle) => _pipelinePool.Destroy(handle);

    public ISwapchain CreateSwapchain(IWindow window)
    {
        // TODO: resolve KhrSwapchain extension from _logicalDevice
        KhrSwapchain khrSwapchain = null!;
        return new VulkanSwapchain(
            _instance.Vk,
            _logicalDevice,
            khrSwapchain,
            _surface,
            _texturePool,
            (uint)window.Size.Width, (uint)window.Size.Height);
    }

    public IRenderGraph CreateRenderGraph() =>
        new VulkanRenderGraph(
            _instance.Vk,
            _logicalDevice.Handle,
            _texturePool,
            _pipelinePool,
            _bufferPool,
            _descriptorAllocator,
            _renderPassCache);

    public void WaitIdle()
    {
        // TODO: _instance.Vk.DeviceWaitIdle(_logicalDevice.Handle);
    }

    public void Dispose()
    {
        WaitIdle();

        // TODO: destroy pools' remaining resources, render pass cache entries,
        // then _debugMessenger?.Dispose(), _logicalDevice.Dispose(),
        // destroy _surface, _instance.Dispose()
    }
}