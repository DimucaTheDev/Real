using System;
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
using Silk.NET.Vulkan.Extensions.EXT;
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
    private readonly VulkanUploadContext _uploadContext;
    private readonly VulkanBufferPool _bufferPool;
    private readonly VulkanTexturePool _texturePool;
    private readonly VulkanSamplerPool _samplerPool;
    private readonly VulkanShaderCompiler _shaderCompiler;
    private readonly VulkanShaderPool _shaderPool;
    private readonly VulkanDescriptorAllocator _descriptorAllocator;
    private readonly VulkanRenderPassCache _renderPassCache;
    private readonly VulkanPipelinePool _pipelinePool;

    private VulkanSwapchain? _currentSwapchain;
    private VulkanRenderGraph? _renderGraph;
    private CommandPool _fallbackCommandPool;
    private CommandBuffer _fallbackCommandBuffer;

    public VulkanDevice(IWindow window, bool enableValidation = false)
    {
        if (window.GraphicsApi != GraphicsApi.Vulkan)
            throw new InvalidOperationException(
                "VulkanDevice requires a window created with WindowSettings.GraphicsApi = GraphicsApi.Vulkan.");

        if (window.Handle == 0)
        {
            window.Show();
        }

        _instance = new VulkanInstance(appName: "RealEngine", enableValidation);

        _surface = SurfaceFactory.CreateForWindow(_instance.Vk, _instance.Handle, window.Handle);

        var physicalDevice = VulkanPhysicalDeviceSelector.Select(_instance, _surface);
        _logicalDevice = new VulkanLogicalDevice(_instance.Vk, _instance.Handle, physicalDevice, _surface);

        if (enableValidation)
        {
            if (_instance.Vk.TryGetInstanceExtension<ExtDebugUtils>(_instance.Handle, out var debugUtils))
            {
                _debugMessenger = new VulkanDebugMessenger(_instance.Vk, _instance.Handle, debugUtils);
            }
        }

        _memoryAllocator =
            new VulkanMemoryAllocator(_instance.Vk, _logicalDevice.PhysicalDevice, _logicalDevice.Handle);
        _uploadContext = new VulkanUploadContext(
            _instance.Vk,
            _logicalDevice.Handle,
            _logicalDevice.Queues.TransferQueue,
            _logicalDevice.Queues.TransferFamilyIndex);

        _bufferPool = new VulkanBufferPool(
            _instance.Vk,
            _logicalDevice.Handle,
            _memoryAllocator,
            _logicalDevice.Queues.TransferQueue,
            _logicalDevice.Queues.TransferFamilyIndex);

        _texturePool = new VulkanTexturePool(
            _instance.Vk,
            _logicalDevice.Handle,
            _memoryAllocator,
            _uploadContext);

        _samplerPool = new VulkanSamplerPool(_instance.Vk, _logicalDevice.Handle);
        _shaderCompiler = new VulkanShaderCompiler(_instance.Vk, _logicalDevice.Handle);
        _shaderPool = new VulkanShaderPool(_instance.Vk, _logicalDevice.Handle);
        _descriptorAllocator = new VulkanDescriptorAllocator(_instance.Vk, _logicalDevice.Handle);
        _renderPassCache = new VulkanRenderPassCache(_instance.Vk, _logicalDevice.Handle);
        _pipelinePool = new VulkanPipelinePool(_instance.Vk, _logicalDevice.Handle, _renderPassCache, _shaderPool);
    }

    public BufferHandle CreateBuffer(in BufferDescriptor descriptor, ReadOnlySpan<byte> initialData = default) =>
        _bufferPool.Create(descriptor, initialData);

    public void DestroyBuffer(BufferHandle handle) => _bufferPool.Destroy(handle);

    public TextureHandle CreateTexture(in TextureDescriptor descriptor, ReadOnlySpan<byte> initialData = default) =>
        _texturePool.Create(descriptor, initialData);

    public void DestroyTexture(TextureHandle handle) => _texturePool.Destroy(handle);

    public SamplerHandle CreateSampler(in SamplerDescriptor descriptor) =>
        _samplerPool.Create(descriptor);

    public void DestroySampler(SamplerHandle handle) =>
        _samplerPool.Destroy(handle);

    public ShaderHandle CreateShader(ShaderStage stage, ReadOnlySpan<byte> bytecode, string entryPoint = "main")
    {
        var module = _shaderCompiler.CreateModule(bytecode, stage, entryPoint);
        return _shaderPool.Store(module, stage, entryPoint);
    }

    public void DestroyShader(ShaderHandle handle) =>
        _shaderPool.Destroy(handle);

    public PipelineHandle CreatePipeline(in PipelineDescriptor descriptor) =>
        _pipelinePool.Create(descriptor);

    public void DestroyPipeline(PipelineHandle handle) => _pipelinePool.Destroy(handle);

    public ISwapchain CreateSwapchain(IWindow window)
    {
        if (!_instance.Vk.TryGetDeviceExtension<KhrSwapchain>(_instance.Handle, _logicalDevice.Handle,
                out var khrSwapchain))
            throw new InvalidOperationException("VK_KHR_swapchain is not available on this device.");

        if (!_instance.Vk.TryGetInstanceExtension<KhrSurface>(_instance.Handle, out var khrSurface))
            throw new InvalidOperationException("VK_KHR_surface is not available on this instance.");

        var swapchain = new VulkanSwapchain(
            _instance.Vk,
            _logicalDevice,
            khrSwapchain,
            khrSurface,
            _surface,
            _texturePool,
            _renderPassCache,
            _descriptorAllocator,
            (uint)window.Size.Width,
            (uint)window.Size.Height);

        _currentSwapchain = swapchain;
        return swapchain;
    }

    public IRenderGraph CreateRenderGraph()
    {
        if (_renderGraph == null)
        {
            _renderGraph = new VulkanRenderGraph(
                _instance.Vk,
                _logicalDevice,
                _texturePool,
                _pipelinePool,
                _bufferPool,
                _samplerPool,
                _descriptorAllocator,
                _renderPassCache,
                () => _currentSwapchain,
                GetFallbackCommandBuffer);
        }
        else
        {
            _renderGraph.Reset();
        }
        return _renderGraph;
    }

    private unsafe CommandBuffer GetFallbackCommandBuffer()
    {
        if (_fallbackCommandPool.Handle == 0)
        {
            var poolInfo = new CommandPoolCreateInfo
            {
                SType = StructureType.CommandPoolCreateInfo,
                Flags = CommandPoolCreateFlags.ResetCommandBufferBit,
                QueueFamilyIndex = _logicalDevice.Queues.GraphicsFamilyIndex
            };
            var poolRes =
                _instance.Vk.CreateCommandPool(_logicalDevice.Handle, in poolInfo, null, out _fallbackCommandPool);
            if (poolRes != Result.Success)
                throw new InvalidOperationException($"vkCreateCommandPool failed: {poolRes}");

            var allocInfo = new CommandBufferAllocateInfo
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = _fallbackCommandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };
            _instance.Vk.AllocateCommandBuffers(_logicalDevice.Handle, in allocInfo, out _fallbackCommandBuffer);
        }

        return _fallbackCommandBuffer;
    }

    public void WaitIdle()
    {
        _instance.Vk.DeviceWaitIdle(_logicalDevice.Handle);
    }

    public unsafe void Dispose()
    {
        WaitIdle();

        _currentSwapchain?.Dispose();
        _currentSwapchain = null;

        if (_fallbackCommandPool.Handle != 0)
        {
            _instance.Vk.DestroyCommandPool(_logicalDevice.Handle, _fallbackCommandPool, null);
            _fallbackCommandPool = default;
            _fallbackCommandBuffer = default;
        }

        _pipelinePool.Dispose();
        _shaderPool.Dispose();
        _samplerPool.Dispose();
        _descriptorAllocator.Dispose();
        _renderPassCache.Dispose();

        _texturePool.Dispose();
        _bufferPool.Dispose();
        _uploadContext.Dispose();

        _debugMessenger?.Dispose();
        _logicalDevice.Dispose();

        if (_instance.Vk.TryGetInstanceExtension<KhrSurface>(_instance.Handle, out var khrSurface))
        {
            khrSurface.DestroySurface(_instance.Handle, _surface, null);
        }

        _instance.Dispose();
    }
}