using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.Core;

/// <summary>
/// Resolved queue family indices + queue handles for a logical device.
/// Graphics and present are often the same family but not guaranteed to be.
/// </summary>
internal sealed class VulkanQueueSet
{
    public uint GraphicsFamilyIndex { get; init; }
    public uint PresentFamilyIndex { get; init; }
    public uint TransferFamilyIndex { get; init; }

    public Queue GraphicsQueue { get; init; }
    public Queue PresentQueue { get; init; }
    public Queue TransferQueue { get; init; }

    public bool GraphicsAndPresentShareFamily => GraphicsFamilyIndex == PresentFamilyIndex;
}
