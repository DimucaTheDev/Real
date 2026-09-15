using Silk.NET.Vulkan;

namespace Real.Graphics.Vulkan.Sync;

/// <summary>Reusable pool of VkFence objects to avoid creating one per frame forever.</summary>
internal sealed class VulkanFencePool
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly Stack<Fence> _available = new();

    public VulkanFencePool(Vk vk, Device device)
    {
        _vk = vk;
        _device = device;
    }

    public unsafe Fence Rent()
    {
        if (_available.Count > 0)
        {
            var fence = _available.Pop();
            _vk.ResetFences(_device, 1, in fence);
            return fence;
        }

        FenceCreateInfo info = new FenceCreateInfo()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
        };
        _vk.CreateFence(_device, in info, null, out var pFence);
        return pFence;
    }

    public void Return(Fence fence) => _available.Push(fence);
}
