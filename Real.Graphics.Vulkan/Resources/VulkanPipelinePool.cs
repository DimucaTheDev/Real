using System;
using System.Collections.Generic;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Real.Graphics.Vulkan.RenderGraph;
using Real.Graphics.Vulkan.Translation;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using PrimitiveTopology = Real.Graphics.Rhi.Enums.PrimitiveTopology;

namespace Real.Graphics.Vulkan.Resources;

internal readonly record struct VulkanPipelineEntry(
    Pipeline Handle,
    PipelineLayout Layout,
    DescriptorSetLayout DescriptorSetLayout);

internal sealed class VulkanPipelinePool : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;
    private readonly VulkanRenderPassCache _renderPassCache;
    private readonly VulkanShaderPool _shaderPool;

    private readonly List<VulkanPipelineEntry?> _slots = new();
    private readonly List<uint> _generations = new();
    private readonly Queue<uint> _freeSlots = new();

    public VulkanPipelinePool(
        Vk vk,
        Device device,
        VulkanRenderPassCache renderPassCache,
        VulkanShaderPool shaderPool)
    {
        _vk = vk;
        _device = device;
        _renderPassCache = renderPassCache;
        _shaderPool = shaderPool;

        // Reserve slot 0 so valid handles always have Id > 0 (PipelineHandle.Invalid is (0, 0))
        _slots.Add(null);
        _generations.Add(0);
    }

    public unsafe PipelineHandle Create(in PipelineDescriptor descriptor)
    {
        var vsEntry = _shaderPool.Get(descriptor.VertexShader);
        var fsEntry = _shaderPool.Get(descriptor.FragmentShader);

        // 1. Create DescriptorSetLayout
        var setLayoutInfo = new DescriptorSetLayoutCreateInfo
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 0,
            PBindings = null
        };
        var res = _vk.CreateDescriptorSetLayout(_device, in setLayoutInfo, null, out var descriptorSetLayout);
        if (res != Result.Success)
            throw new InvalidOperationException($"vkCreateDescriptorSetLayout failed: {res}");

        // 2. Create PipelineLayout with push constant range and descriptor set layout
        var pushConstantRange = new PushConstantRange
        {
            StageFlags = ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit,
            Offset = 0,
            Size = 128
        };
        var setLayoutCopy = descriptorSetLayout;
        var pipelineLayoutInfo = new PipelineLayoutCreateInfo
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = &setLayoutCopy,
            PushConstantRangeCount = 1,
            PPushConstantRanges = &pushConstantRange
        };
        res = _vk.CreatePipelineLayout(_device, in pipelineLayoutInfo, null, out var pipelineLayout);
        if (res != Result.Success)
            throw new InvalidOperationException($"vkCreatePipelineLayout failed: {res}");

        // 3. Shader stages
        var pVsName = (byte*)SilkMarshal.StringToPtr(vsEntry.EntryPoint);
        var pFsName = (byte*)SilkMarshal.StringToPtr(fsEntry.EntryPoint);

        var stages = stackalloc PipelineShaderStageCreateInfo[2];
        stages[0] = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vsEntry.Module,
            PName = pVsName
        };
        stages[1] = new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fsEntry.Module,
            PName = pFsName
        };

        // 4. Vertex input
        var bindingDesc = new VertexInputBindingDescription
        {
            Binding = 0,
            Stride = descriptor.VertexLayout.Stride,
            InputRate = VertexInputRate.Vertex
        };

        var attrs = descriptor.VertexLayout.Attributes ?? Array.Empty<VertexAttribute>();
        var attrDescs = stackalloc VertexInputAttributeDescription[attrs.Length];
        for (int i = 0; i < attrs.Length; i++)
        {
            attrDescs[i] = new VertexInputAttributeDescription
            {
                Location = attrs[i].Location,
                Binding = 0,
                Format = VkFormatMap.ToVkVertexFormat(attrs[i].Format),
                Offset = attrs[i].Offset
            };
        }

        var vertexInputState = new PipelineVertexInputStateCreateInfo
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = attrs.Length > 0 ? 1u : 0u,
            PVertexBindingDescriptions = attrs.Length > 0 ? &bindingDesc : null,
            VertexAttributeDescriptionCount = (uint)attrs.Length,
            PVertexAttributeDescriptions = attrs.Length > 0 ? attrDescs : null
        };

        // 5. Input assembly
        var inputAssembly = new PipelineInputAssemblyStateCreateInfo
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = ToVkTopology(descriptor.Topology),
            PrimitiveRestartEnable = false
        };

        // 6. Viewport & Scissor (dynamic)
        var dynamicStates = stackalloc DynamicState[]
        {
            DynamicState.Viewport,
            DynamicState.Scissor
        };
        var dynamicStateCreateInfo = new PipelineDynamicStateCreateInfo
        {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = 2,
            PDynamicStates = dynamicStates
        };

        var viewportState = new PipelineViewportStateCreateInfo
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            ScissorCount = 1
        };

        // 7. Rasterization, Multisample, DepthStencil, ColorBlend
        var rasterState = VkRasterStateTranslator.ToVk(descriptor.Raster);
        var multisampleState = new PipelineMultisampleStateCreateInfo
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            RasterizationSamples = SampleCountFlags.Count1Bit
        };
        var depthStencilState = VkDepthStencilTranslator.ToVk(descriptor.DepthStencil);

        var blendCount = descriptor.ColorAttachmentFormats.Length;
        var blendAttachments = stackalloc PipelineColorBlendAttachmentState[blendCount];
        for (int i = 0; i < blendCount; i++)
        {
            blendAttachments[i] = VkBlendStateTranslator.ToVk(descriptor.Blend);
        }
        var colorBlendState = new PipelineColorBlendStateCreateInfo
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = (uint)blendCount,
            PAttachments = blendAttachments
        };

        // 8. RenderPass
        var renderPass = _renderPassCache.GetOrCreate(descriptor.ColorAttachmentFormats, descriptor.DepthAttachmentFormat);

        // 9. Pipeline
        var pipelineInfo = new GraphicsPipelineCreateInfo
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            StageCount = 2,
            PStages = stages,
            PVertexInputState = &vertexInputState,
            PInputAssemblyState = &inputAssembly,
            PViewportState = &viewportState,
            PRasterizationState = &rasterState,
            PMultisampleState = &multisampleState,
            PDepthStencilState = &depthStencilState,
            PColorBlendState = &colorBlendState,
            PDynamicState = &dynamicStateCreateInfo,
            Layout = pipelineLayout,
            RenderPass = renderPass,
            Subpass = 0
        };

        try
        {
            res = _vk.CreateGraphicsPipelines(_device, default, 1, in pipelineInfo, null, out var pipeline);
            if (res != Result.Success)
                throw new InvalidOperationException($"vkCreateGraphicsPipelines failed: {res}");

            return Store(pipeline, pipelineLayout, descriptorSetLayout);
        }
        finally
        {
            SilkMarshal.Free((nint)pVsName);
            SilkMarshal.Free((nint)pFsName);
        }
    }

    private static Silk.NET.Vulkan.PrimitiveTopology ToVkTopology(PrimitiveTopology topology) => topology switch
    {
        PrimitiveTopology.TriangleList => Silk.NET.Vulkan.PrimitiveTopology.TriangleList,
        PrimitiveTopology.TriangleStrip => Silk.NET.Vulkan.PrimitiveTopology.TriangleStrip,
        PrimitiveTopology.LineList => Silk.NET.Vulkan.PrimitiveTopology.LineList,
        PrimitiveTopology.PointList => Silk.NET.Vulkan.PrimitiveTopology.PointList,
        _ => Silk.NET.Vulkan.PrimitiveTopology.TriangleList
    };

    public bool IsValid(PipelineHandle handle) =>
        handle.Id != 0 &&
        handle.Id < _slots.Count &&
        _slots[(int)handle.Id] is not null &&
        _generations[(int)handle.Id] == handle.Generation;

    public VulkanPipelineEntry Get(PipelineHandle handle)
    {
        if (!IsValid(handle))
            throw new ArgumentException($"Invalid or expired pipeline handle: {handle}");
        return _slots[(int)handle.Id]!.Value;
    }

    public unsafe void Destroy(PipelineHandle handle)
    {
        if (!IsValid(handle)) return;

        var entry = _slots[(int)handle.Id]!.Value;
        _vk.DestroyPipeline(_device, entry.Handle, null);
        _vk.DestroyPipelineLayout(_device, entry.Layout, null);
        _vk.DestroyDescriptorSetLayout(_device, entry.DescriptorSetLayout, null);

        _slots[(int)handle.Id] = null;
        _generations[(int)handle.Id]++;
        _freeSlots.Enqueue(handle.Id);
    }

    private PipelineHandle Store(Pipeline pipeline, PipelineLayout layout, DescriptorSetLayout setLayout)
    {
        uint id;
        if (_freeSlots.Count > 0)
        {
            id = _freeSlots.Dequeue();
            _slots[(int)id] = new VulkanPipelineEntry(pipeline, layout, setLayout);
        }
        else
        {
            id = (uint)_slots.Count;
            _slots.Add(new VulkanPipelineEntry(pipeline, layout, setLayout));
            _generations.Add(0);
        }

        return new PipelineHandle(id, _generations[(int)id]);
    }

    public unsafe void Dispose()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] is { } entry)
            {
                _vk.DestroyPipeline(_device, entry.Handle, null);
                _vk.DestroyPipelineLayout(_device, entry.Layout, null);
                _vk.DestroyDescriptorSetLayout(_device, entry.DescriptorSetLayout, null);
                _slots[i] = null;
            }
        }
        _freeSlots.Clear();
    }
}
