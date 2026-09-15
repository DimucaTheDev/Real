using Real.Graphics.Rhi;
using Real.Windowing;

namespace Real.Graphics.Vulkan;

/// <summary>
/// The only type a composition root needs to reference to obtain a Vulkan
/// IGraphicsDevice - see the earlier discussion on keeping Real.Core free
/// of any reference to Real.Rhi.Vulkan itself.
/// </summary>
public sealed class VulkanBackendFactory : IGraphicsBackendFactory
{
    public GraphicsApi Api => GraphicsApi.Vulkan;

    public IGraphicsDevice CreateDevice(IWindow window, bool enableValidation = false) =>
        new VulkanDevice(window, enableValidation);
}
