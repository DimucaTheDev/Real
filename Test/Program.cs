using System.Runtime.InteropServices;
using System.Threading;
using Real.Graphics.OpenGL;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Vulkan;
using Real.Windowing;
using Real.Windowing.Glfw;

namespace Test;

unsafe class Program
{
    private static IWindow window = null!;

    private struct Vertex
    {
        public float X, Y;
        public float R, G, B;
    }

    static void Main()
    {
        window = new GlfwWindow(GraphicsApi.OpenGl);
        window.Show();
        var factory = new OpenGlBackendFactory();
        using var device = factory.CreateDevice(window, true);
        using var swapchain = device.CreateSwapchain(window);
        ReadOnlySpan<Vertex> triangle =
        [
            new Vertex { X = 0.0f, Y = -0.5f, R = 1, G = 0, B = 0 },
            new Vertex { X = 0.5f, Y = 0.5f, R = 0, G = 1, B = 0 },
            new Vertex { X = -0.5f, Y = 0.5f, R = 0, G = 0, B = 1 }
        ];
        var vertexBuffer = device.CreateBuffer(
            new BufferDescriptor(
                Size: (ulong)(triangle.Length * Marshal.SizeOf<Vertex>()),
                Usage: BufferUsage.Vertex,
                DebugName: "TriangleVertexBuffer"), MemoryMarshal.AsBytes(triangle));
        var vertexShader = device.CreateShader(ShaderStage.Vertex, File.ReadAllBytes("shaders/main.vert"));
        var fragmentShader = device.CreateShader(ShaderStage.Fragment, File.ReadAllBytes("shaders/main.frag"));
        var pipeline = device.CreatePipeline(new PipelineDescriptor()
        {
            VertexShader = vertexShader,
            FragmentShader = fragmentShader,
            Topology = PrimitiveTopology.TriangleList,
            Blend = BlendState.Opaque,
            ColorAttachmentFormats = [swapchain.Format],
            DebugName = "testPipeline",
            DepthAttachmentFormat = null,
            DepthStencil = DepthStencilState.Disabled,
            Raster = new RasterState(CullMode.None),
            VertexLayout = new VertexLayout()
            {
                Stride = (uint)Marshal.SizeOf<Vertex>(),
                Attributes =
                [
                    new VertexAttribute(Location: 0, Format: TextureFormat.Rg8Unorm, Offset: 0),
                    new VertexAttribute(Location: 1, Format: TextureFormat.Rgba8Unorm, Offset: 8),
                ]
            },
        });
        while (!window.IsClosing)
        {
            window.PollEvents();
            if (window.Size.Width <= 0 || window.Size.Height <= 0)
            {
                Thread.Sleep(16);
                continue;
            }
            var backbuffer = swapchain.AcquireNextImage();
            if (!backbuffer.IsValid)
            {
                Thread.Sleep(16);
                continue;
            }
            var graph = device.CreateRenderGraph();
            graph
                .AddPass("TestPass")
                .Writes(backbuffer)
                .SetExecute(cmd =>
                {
                    cmd.SetViewport(0, 0, window.Size.Width, window.Size.Height);
                    cmd.SetScissor(0, 0, (uint)window.Size.Width, (uint)window.Size.Height);

                    cmd.BindPipeline(pipeline);
                    cmd.BindVertexBuffer(vertexBuffer);
                    cmd.Draw(vertexCount: 3);
                });
            graph.Execute();
            swapchain.Present();
        }

        device.WaitIdle();
        device.DestroyPipeline(pipeline);
        device.DestroyShader(vertexShader);
        device.DestroyShader(fragmentShader);
        device.DestroyBuffer(vertexBuffer);
    }
#if false
    static unsafe void Main2()
    {
        vk = Vk.GetApi();

        var appInfo = new ApplicationInfo()
        {
            PApplicationName = (byte*)SilkMarshal.StringToPtr("app name"),
            ApplicationVersion = Vk.MakeVersion(1, 2, 3),
            ApiVersion = Vk.Version13,
        };

        window = new GlfwWindow(GraphicsApi.Vulkan);
        window.Show();

        var instanceLayers = GetInstanceLayers();
        if (Debugger.IsAttached && !instanceLayers.Contains("VK_LAYER_KHRONOS_validation"))
            throw new();

        var debug = new DebugUtilsMessengerCreateInfoEXT()
        {
            MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
            MessageType = DebugUtilsMessageTypeFlagsEXT.ValidationBitExt,
            PfnUserCallback = new PfnDebugUtilsMessengerCallbackEXT((_, _, data, _) =>
            {
                Console.WriteLine("Validation layer: " + SilkMarshal.PtrToString((nint)data->PMessage));
                return Vk.False;
            }),
            SType = StructureType.DebugUtilsMessengerCreateInfoExt,
            PUserData = null
        };
        List<string> initExs =
        [
            .. SilkMarshal.PtrToStringArray((nint)Glfw.GetApi().GetRequiredInstanceExtensions(out var glfwExCount),
                (int)glfwExCount)
        ];
        if (Debugger.IsAttached)
            initExs.Add("VK_EXT_debug_utils");
        var instanceCreateInfo = new InstanceCreateInfo()
        {
            PApplicationInfo = &appInfo,
            EnabledLayerCount = Debugger.IsAttached ? 1 : (uint)0,
            SType = StructureType.InstanceCreateInfo,
            PpEnabledLayerNames = Debugger.IsAttached
                ? (byte**)SilkMarshal.StringArrayToPtr(["VK_LAYER_KHRONOS_validation"])
                : null,
            EnabledExtensionCount = (uint)initExs.Count,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(initExs),
            PNext = &debug
        };

        if (vk.CreateInstance(in instanceCreateInfo, null, out var instance) != Result.Success)
        {
            throw new();
        }

        var surfaceKhr = CreateSurface(instance);

        var devices = GetPhysicalDevices(instance);
        if (!devices.Any())
            throw new();

        const string ex = KhrSwapchain.ExtensionName;
        List<int> graphicFamilyIndices = [];
        List<int> presentationFamilyIndices = [];
        PhysicalDevice physDevice = default;

        foreach (var dev in GetPhysicalDevices(instance))
        {
            var supports = GetDeviceExtensions(dev).Contains(ex);
            List<int> physDeviceGraphicFamilyIndices = [];
            List<int> physDevicePresentationFamilyIndices = [];
            var props = GetDeviceQueueProps(dev);
            for (int i = 0; i < props.Length; i++)
            {
                if ((props[i].QueueFlags & QueueFlags.GraphicsBit) != 0)
                    physDeviceGraphicFamilyIndices.Add(i);
                physDevicePresentationFamilyIndices.Add(i);
                i++;
            }

            if (supports && physDevicePresentationFamilyIndices.Any() && physDeviceGraphicFamilyIndices.Any())
            {
                physDevice = dev;
                graphicFamilyIndices = physDeviceGraphicFamilyIndices;
                presentationFamilyIndices = physDevicePresentationFamilyIndices;
                break;
            }
        }

        if (physDevice.Handle == 0)
            throw new();


        List<DeviceQueueCreateInfo> deviceQueueCreateInfos = [];
        float[] queuePriorities = [1, 1];
        var graphicQueueIndex = (0, 0);
        var presentationQueueIndex = (0, 0);

        bool configured = false;
        foreach (var graphicFamilyIndex in graphicFamilyIndices)
        {
            foreach (var presentationFamilyIndex in presentationFamilyIndices)
            {
                if (graphicFamilyIndex == presentationFamilyIndex)
                {
                    fixed (float* d = queuePriorities)
                        deviceQueueCreateInfos.Add(new()
                        {
                            SType = StructureType.DeviceQueueCreateInfo,
                            QueueFamilyIndex = (uint)graphicFamilyIndex,
                            QueueCount = 1,
                            PQueuePriorities = d
                        });
                    graphicQueueIndex = (graphicFamilyIndex, 0);
                    presentationQueueIndex = (presentationFamilyIndex, 0);
                    configured = true;
                    break;
                }
            }

            if (configured) break;
        }

        if (!configured)
        {
            fixed (float* d = queuePriorities)
            {
                graphicQueueIndex = (graphicFamilyIndices[0], 0);
                deviceQueueCreateInfos.Add(new()
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = (uint)graphicFamilyIndices[0],
                    QueueCount = 1,
                    PQueuePriorities = d
                });
                presentationQueueIndex = (presentationFamilyIndices[0], 0);
                deviceQueueCreateInfos.Add(new()
                {
                    SType = StructureType.DeviceQueueCreateInfo,
                    QueueFamilyIndex = (uint)presentationFamilyIndices[0],
                    QueueCount = 1,
                    PQueuePriorities = d
                });
            }
        }

        Device logicalDevice = default;
        fixed (DeviceQueueCreateInfo* i = deviceQueueCreateInfos.ToArray())
        {
            DeviceCreateInfo c = new()
            {
                SType = StructureType.DeviceCreateInfo,
                QueueCreateInfoCount = (uint)deviceQueueCreateInfos.Count,
                PQueueCreateInfos = i,
                EnabledExtensionCount = 1,
                PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr([KhrSwapchain.ExtensionName])
            };
            var c_ = vk.CreateDevice(physDevice, &c, null, &logicalDevice);
            if (c_ != Result.Success)
            {
                throw new();
            }
        }

        var graphicQueue =
            vk.GetDeviceQueue(logicalDevice, (uint)graphicQueueIndex.Item1, (uint)graphicQueueIndex.Item2);
        var presentationQueue = vk.GetDeviceQueue(logicalDevice, (uint)presentationQueueIndex.Item1,
            (uint)presentationQueueIndex.Item2);


        if (!vk.TryGetInstanceExtension<KhrSurface>(instance, out var ext))
        {
            throw new();
        }

        SurfaceCapabilitiesKHR cap = default;
        if (ext.GetPhysicalDeviceSurfaceCapabilities(physDevice, surfaceKhr, &cap) != Result.Success)
        {
            throw new();
        }

        uint formatCount = 0;
        ext.GetPhysicalDeviceSurfaceFormats(physDevice, surfaceKhr, &formatCount, null);
        var formats = new SurfaceFormatKHR[formatCount];
        fixed (SurfaceFormatKHR* f = formats)
            if (ext.GetPhysicalDeviceSurfaceFormats(physDevice, surfaceKhr, &formatCount, f) != Result.Success)
            {
                throw new();
            }


        uint presCount = 0;
        ext.GetPhysicalDeviceSurfacePresentModes(physDevice, surfaceKhr, &presCount, null);
        var presFormats = new PresentModeKHR[presCount];
        fixed (PresentModeKHR* f = presFormats)
            if (ext.GetPhysicalDeviceSurfacePresentModes(physDevice, surfaceKhr, &presCount, f) != Result.Success)
            {
                throw new();
            }


        var swapchainImageFormat = formats.First().Format;
        var swapchainColorSpace = formats.First().ColorSpace;
        var swapchainExtent = cap.CurrentExtent;
        var swapchainPresentMode =
            presFormats.Contains(PresentModeKHR.FifoKhr) ? PresentModeKHR.FifoKhr : presFormats.First();

        if (!vk.TryGetDeviceExtension<KhrSwapchain>(instance, logicalDevice, out var swapchainExt))
        {
            throw new();
        }

        var swInfo = new SwapchainCreateInfoKHR()
        {
            Surface = surfaceKhr,
            MinImageCount = cap.MinImageCount,
            ImageFormat = swapchainImageFormat,
            ImageColorSpace = swapchainColorSpace,
            ImageExtent = swapchainExtent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = SharingMode.Exclusive,
            QueueFamilyIndexCount = 0,
            PQueueFamilyIndices = null,
            PreTransform = cap.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = swapchainPresentMode,
            Clipped = true,
            OldSwapchain = default,
            SType = StructureType.SwapchainCreateInfoKhr
        };
        SwapchainKHR swapchainKhr = default;
        if (swapchainExt.CreateSwapchain(logicalDevice, &swInfo, null, &swapchainKhr) != Result.Success)
        {
            throw new();
        }

        uint imgCount = 0;
        swapchainExt.GetSwapchainImages(logicalDevice, swapchainKhr, &imgCount, null);
        MediaTypeNames.Image[] images = new MediaTypeNames.Image[imgCount];
        fixed (MediaTypeNames.Image* i = images)
            if (swapchainExt.GetSwapchainImages(logicalDevice, swapchainKhr, &imgCount, i) != Result.Success)
            {
                throw new();
            }


        List<ImageView> imageViews = [with(capacity: images.Length)];
        foreach (var image in images)
        {
            var inf = new ImageViewCreateInfo()
            {
                Image = image,
                ViewType = ImageViewType.Type2D,
                Format = swapchainImageFormat,
                Components = new ComponentMapping
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity,
                },
                SubresourceRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                },
                SType = StructureType.ImageViewCreateInfo
            };
            if (vk.CreateImageView(logicalDevice, &inf, null, out var view) != Result.Success)
            {
                throw new();
            }

            imageViews.Add(view);
        }

        var swapchainAttachmentDescription = new AttachmentDescription()
        {
            Format = swapchainImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };
        var swapchainSubpassReference = new AttachmentReference()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };
        var swapchainSubpassDescription = new SubpassDescription()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &swapchainSubpassReference
        };
        var swapchainSubpassDependencies = new SubpassDependency[]
        {
            new SubpassDependency
            {
                SrcSubpass = Vk.SubpassExternal,
                DstSubpass = 0,
                SrcStageMask = PipelineStageFlags.TopOfPipeBit,
                DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                SrcAccessMask = 0,
                DstAccessMask = AccessFlags.ColorAttachmentWriteBit,
                DependencyFlags = DependencyFlags.ByRegionBit
            },
            new SubpassDependency
            {
                SrcSubpass = 0,
                DstSubpass = Vk.SubpassExternal,
                SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit,
                DstStageMask = PipelineStageFlags.BottomOfPipeBit,
                SrcAccessMask = AccessFlags.ColorAttachmentWriteBit,
                DstAccessMask = 0,
                DependencyFlags = DependencyFlags.ByRegionBit
            }
        };
        RenderPass pass;
        fixed (SubpassDependency* d = swapchainSubpassDependencies)
        {
            var rpInfo = new RenderPassCreateInfo()
            {
                AttachmentCount = 1,
                PAttachments = &swapchainAttachmentDescription,
                SubpassCount = 1,
                PSubpasses = &swapchainSubpassDescription,
                DependencyCount = (uint)swapchainSubpassDependencies.Length,
                PDependencies = d,
                SType = StructureType.RenderPassCreateInfo
            };
            if (vk.CreateRenderPass(logicalDevice, &rpInfo, null, out pass) != Result.Success)
            {
                throw new();
            }
        }

        List<Framebuffer> framebuffers = [with(capacity: (int)imgCount)];
        foreach (var imageView in imageViews)
        {
            var ffInfo = new FramebufferCreateInfo
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = pass,
                AttachmentCount = 1,
                PAttachments = &imageView,
                Width = swapchainExtent.Width,
                Height = swapchainExtent.Height,
                Layers = 1
            };
            if (vk.CreateFramebuffer(logicalDevice, &ffInfo, null, out var framebuffer) != Result.Success)
            {
                throw new();
            }

            framebuffers.Add(framebuffer);
        }

        var vertices = new[]
        {
            0.0f, -0.5f, 1.0f, 0.0f, 0.0f,
            0.5f, 0.5f, 0.0f, 1.0f, 0.0f,
            -0.5f, 0.5f, 0.0f, 0.0f, 1.0f
        };

        var bci = new BufferCreateInfo
        {
            SType = StructureType.BufferCreateInfo,
            Size = (ulong)(sizeof(float) * vertices.Length),
            Usage = BufferUsageFlags.VertexBufferBit,
            SharingMode = SharingMode.Exclusive
        };
        if (vk.CreateBuffer(logicalDevice, &bci, null, out var buffer) != Result.Success)
        {
            throw new();
        }

        var bufferReqs = vk.GetBufferMemoryRequirements(logicalDevice, buffer);
        var memoryProps = vk.GetPhysicalDeviceMemoryProperties(physDevice);
        int? vertexBufferMemoryTypeIndex = null;
        for (int memoryTypeIndex = 0; memoryTypeIndex < memoryProps.MemoryTypeCount; memoryTypeIndex++)
        {
            if ((bufferReqs.MemoryTypeBits & 1 << memoryTypeIndex) != 0 &&
                (memoryProps.MemoryTypes[memoryTypeIndex].PropertyFlags & MemoryPropertyFlags.HostVisibleBit) != 0 &&
                (memoryProps.MemoryTypes[memoryTypeIndex].PropertyFlags & MemoryPropertyFlags.HostCoherentBit) != 0)
            {
                vertexBufferMemoryTypeIndex = memoryTypeIndex;
                break;
            }
        }

        if (!vertexBufferMemoryTypeIndex.HasValue)
            throw new();


        var memAllocInfo = new MemoryAllocateInfo()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = bufferReqs.Size,
            MemoryTypeIndex = (uint)vertexBufferMemoryTypeIndex
        };
        if (vk.AllocateMemory(logicalDevice, &memAllocInfo, null, out var memory) != Result.Success)
        {
            throw new();
        }

        if (vk.BindBufferMemory(logicalDevice, buffer, memory, 0) != Result.Success)
        {
            throw new();
        }

        void* data;
        if (vk.MapMemory(logicalDevice, memory, (ulong)0, bufferReqs.Size, MemoryMapFlags.None, &data) !=
            Result.Success)
        {
            throw new();
        }

        Marshal.Copy(vertices, 0, (nint)data, vertices.Length);
        vk.UnmapMemory(logicalDevice, memory);


        var vert = CreateShaderModule(vk, logicalDevice, "shaders/triangle.vert.spv");
        var frag = CreateShaderModule(vk, logicalDevice, "shaders/triangle.frag.spv");

        PipelineLayout pipelineLayout = default;
        var pipelineInfo = new PipelineLayoutCreateInfo()
        {
            SType = StructureType.PipelineLayoutCreateInfo
        };
        if (vk.CreatePipelineLayout(logicalDevice, &pipelineInfo, null, &pipelineLayout) != Result.Success)
        {
            throw new();
        }

        List<PipelineShaderStageCreateInfo> shaderStages =
        [
            new()
            {
                Stage = ShaderStageFlags.VertexBit,
                Module = vert,
                PName = (byte*)SilkMarshal.StringToPtr("main")
            },
            new()
            {
                Stage = ShaderStageFlags.FragmentBit,
                Module = frag,
                PName = (byte*)SilkMarshal.StringToPtr("main")
            }
        ];
        Pipeline pipeline = default;
        fixed (PipelineShaderStageCreateInfo* p = shaderStages.ToArray())
        {
            var vertexInputBinding = new VertexInputBindingDescription()
            {
                Binding = 0,
                Stride = sizeof(float) * 5,
                InputRate = VertexInputRate.Vertex
            };
            var vertexInputAttributes = new VertexInputAttributeDescription[]
            {
                new()
                {
                    Location = 0,
                    Binding = 0,
                    Format = Format.R32G32Sfloat,
                    Offset = 0
                },
                new()
                {
                    Location = 1,
                    Binding = 0,
                    Format = Format.R32G32B32Sfloat,
                    Offset = 2 * sizeof(float)
                }
            };
            fixed (VertexInputAttributeDescription* v = vertexInputAttributes)
            {
                var vertexInputStateCreateInfo = new PipelineVertexInputStateCreateInfo()
                {
                    VertexBindingDescriptionCount = 1,
                    PVertexBindingDescriptions = &vertexInputBinding,
                    VertexAttributeDescriptionCount = 2,
                    PVertexAttributeDescriptions = v
                };
                var inputAssemblyStateCreateInfo = new PipelineInputAssemblyStateCreateInfo()
                {
                    Topology = PrimitiveTopology.TriangleList,
                    PrimitiveRestartEnable = false
                };
                var viewport = new Viewport()
                {
                    X = 0,
                    Y = 0,
                    Width = swapchainExtent.Width,
                    Height = swapchainExtent.Height,
                    MinDepth = 0,
                    MaxDepth = 1,
                };
                var scissor = new Rect2D()
                {
                    Offset = new(0, 0),
                    Extent = swapchainExtent
                };
                var viewPortStateCreateInfo = new PipelineViewportStateCreateInfo()
                {
                    ViewportCount = 1,
                    PViewports = &viewport,
                    ScissorCount = 1,
                    PScissors = &scissor
                };
                var rasterizationStateCreateInfo = new PipelineRasterizationStateCreateInfo()
                {
                    DepthClampEnable = false,
                    RasterizerDiscardEnable = false,
                    PolygonMode = PolygonMode.Fill,
                    CullMode = CullModeFlags.BackBit,
                    FrontFace = FrontFace.Clockwise,
                    DepthBiasEnable = false,
                    LineWidth = 1
                };
                var multisampleStateCreateInfo = new PipelineMultisampleStateCreateInfo()
                {
                    RasterizationSamples = SampleCountFlags.Count1Bit,
                    SampleShadingEnable = false
                };
                var depthStencilStateCreateInfo = new PipelineDepthStencilStateCreateInfo()
                {
                    DepthTestEnable = false,
                    StencilTestEnable = false
                };
                var colorBlendAttachmentState = new PipelineColorBlendAttachmentState()
                {
                    BlendEnable = false,
                    ColorWriteMask = ColorComponentFlags.RBit |
                                     ColorComponentFlags.GBit |
                                     ColorComponentFlags.BBit |
                                     ColorComponentFlags.ABit
                };
                var colorBlendStateCreateInfo = new PipelineColorBlendStateCreateInfo()
                {
                    LogicOpEnable = false,
                    AttachmentCount = 1,
                    PAttachments = &colorBlendAttachmentState
                };
                var info = new GraphicsPipelineCreateInfo()
                {
                    SType = StructureType.GraphicsPipelineCreateInfo,
                    StageCount = (uint)shaderStages.Count,
                    PStages = p,
                    PVertexInputState = &vertexInputStateCreateInfo,
                    PInputAssemblyState = &inputAssemblyStateCreateInfo,
                    PViewportState = &viewPortStateCreateInfo,
                    PRasterizationState = &rasterizationStateCreateInfo,
                    PMultisampleState = &multisampleStateCreateInfo,
                    PDepthStencilState = &depthStencilStateCreateInfo,
                    PColorBlendState = &colorBlendStateCreateInfo,
                    PDynamicState = null,
                    Layout = pipelineLayout,
                    RenderPass = pass,
                    Subpass = 0
                };

                Pipeline pp = default;
                if (vk.CreateGraphicsPipelines(logicalDevice, default,
                        new ReadOnlySpan<GraphicsPipelineCreateInfo>(ref info), null, &pp) != Result.Success)
                {
                    throw new();
                }

                ;
                pipeline = pp;
            }
        }

        CommandPoolCreateInfo cpi = new CommandPoolCreateInfo()
        {
            QueueFamilyIndex = (uint)graphicQueueIndex.Item1
        };
        if (vk.CreateCommandPool(logicalDevice, &cpi, null, out var pool) != Result.Success)
        {
            throw new();
        }

        var cbai = new CommandBufferAllocateInfo()
        {
            CommandPool = pool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = imgCount
        };

        CommandBuffer[] cmdBuffers = new CommandBuffer[cbai.CommandBufferCount];
        fixed (CommandBuffer* b = cmdBuffers)
            if (vk.AllocateCommandBuffers(logicalDevice, &cbai, b) != Result.Success)
            {
                throw new();
            }

        var clearValue = new ClearValue() { Color = new ClearColorValue(0, 0, 0, 1) };
        for (int frameIndex = 0; frameIndex < cmdBuffers.Length; frameIndex++)
        {
            var commandBuffer = cmdBuffers[frameIndex];

            var beginInfo = new CommandBufferBeginInfo
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.SimultaneousUseBit
            };

            if (vk.BeginCommandBuffer(commandBuffer, in beginInfo) != Result.Success)
            {
                throw new Exception("Failed to begin recording command buffer");
            }

            var renderPassInfo = new RenderPassBeginInfo
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = pass,
                Framebuffer = framebuffers[frameIndex],
                RenderArea = new Rect2D
                {
                    Offset = new Offset2D { X = 0, Y = 0 },
                    Extent = swapchainExtent
                },
                ClearValueCount = 1,
                PClearValues = &clearValue
            };

            vk.CmdBeginRenderPass(commandBuffer, in renderPassInfo, SubpassContents.Inline);

            vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, pipeline);

            Buffer vertexBufferHandle = buffer;
            ulong offset = 0;
            vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &vertexBufferHandle, &offset);

            uint vertexCount = (uint)(vertices.Length / 5);
            vk.CmdDraw(commandBuffer, vertexCount, 1, 0, 0);

            vk.CmdEndRenderPass(commandBuffer);

            if (vk.EndCommandBuffer(commandBuffer) != Result.Success)
            {
                throw new Exception("Failed to end recording command buffer");
            }
        }

        uint currentFrame = 0;

        var imageAvailableSemaphores = new Semaphore[imgCount];
        var renderFinishedSemaphores = new Semaphore[imgCount];
        var inFlightFences = new Fence[imgCount];

        var semaphoreCreateInfo = new SemaphoreCreateInfo
        {
            SType = StructureType.SemaphoreCreateInfo
        };

        var fenceCreateInfo = new FenceCreateInfo
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags
                .SignaledBit // Fence создается в сигнальном состоянии, чтобы первый кадр не заблокировал поток
        };

        for (int i = 0; i < imgCount; i++)
        {
            if (vk.CreateSemaphore(logicalDevice, in semaphoreCreateInfo, null, out imageAvailableSemaphores[i]) !=
                Result.Success)
            {
                throw new Exception("Failed to create image available semaphore");
            }

            if (vk.CreateSemaphore(logicalDevice, in semaphoreCreateInfo, null, out renderFinishedSemaphores[i]) !=
                Result.Success)
            {
                throw new Exception("Failed to create render finished semaphore");
            }

            if (vk.CreateFence(logicalDevice, in fenceCreateInfo, null, out inFlightFences[i]) != Result.Success)
            {
                throw new Exception("Failed to create in-flight fence");
            }
        }

        PipelineStageFlags pipelineStageFlags = PipelineStageFlags.ColorAttachmentOutputBit;

        // 1. Ожидание и сброс Fence
        var fence = inFlightFences[currentFrame];
        if (vk.WaitForFences(logicalDevice, 1, in fence, true, ulong.MaxValue) != Result.Success)
        {
            throw new Exception("Failed to wait for fence");
        }

        if (vk.ResetFences(logicalDevice, 1, in fence) != Result.Success)
        {
            throw new Exception("Failed to reset fence");
        }

// 2. Получение следующего изображения из Swapchain
        uint currentImageIndex = 0;
// khrSwapchain — полученное ранее расширение KhrSwapchain
        if (swapchainExt.AcquireNextImage(logicalDevice, swapchainKhr, ulong.MaxValue,
                imageAvailableSemaphores[currentFrame], default, ref currentImageIndex) != Result.Success)
        {
            throw new Exception("Failed to acquire next image");
        }

// 3. Отправка буфера команд в графическую очередь
        var waitSemaphore = imageAvailableSemaphores[currentFrame];
        var signalSemaphore = renderFinishedSemaphores[currentImageIndex];
        var commandBuffer_ = cmdBuffers[currentImageIndex];
        PipelineStageFlags waitStage = PipelineStageFlags.ColorAttachmentOutputBit;

        var submitInfo = new SubmitInfo
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &waitSemaphore,
            PWaitDstStageMask = &waitStage,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer_,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = &signalSemaphore
        };

        if (vk.QueueSubmit(graphicQueue, 1, in submitInfo, fence) != Result.Success)
        {
            throw new Exception("Failed to submit draw command buffer");
        }

// 4. Презентация изображения на экран
        var swapchainHandle = swapchainKhr;
        uint imageIndex = currentImageIndex;

        var presentInfo = new PresentInfoKHR
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &signalSemaphore,
            SwapchainCount = 1,
            PSwapchains = &swapchainHandle,
            PImageIndices = &imageIndex
        };

        if (swapchainExt.QueuePresent(presentationQueue, in presentInfo) != Result.Success)
        {
            throw new Exception("Failed to present swapchain image");
        }

        currentFrame = (currentFrame + 1) % imgCount;

        while (!window.IsClosing)
        {
            window.PollEvents();
        }

        Console.WriteLine("Done!");
    }

    static ShaderModule CreateShaderModule(Vk vk, Device device, string filePath)
    {
        // Читаем скомпилированный SPIR-V файл в виде массива байт
        byte[] byteCode = File.ReadAllBytes(filePath);

        if (byteCode.Length % 4 != 0)
        {
            throw new Exception($"Файл {filePath} имеет некорректный размер SPIR-V (должен быть кратен 4 байтам).");
        }

        fixed (byte* pCode = byteCode)
        {
            var createInfo = new ShaderModuleCreateInfo
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)byteCode.Length, // Размер в БАЙТАХ
                PCode = (uint*)pCode // Указатель на uint*
            };

            if (vk.CreateShaderModule(device, in createInfo, null, out var shaderModule) != Result.Success)
            {
                throw new Exception($"Не удалось создать ShaderModule из файла: {filePath}");
            }

            return shaderModule;
        }
    }

    static SurfaceKHR CreateSurface(Instance instance)
    {
        VkNonDispatchableHandle h = new();
        var e = Glfw.GetApi().CreateWindowSurface(instance.ToHandle(), (WindowHandle*)window.Handle, null, &h);
        if (h.Handle == 0)
            throw new();
        return h.ToSurface();
    }

    static PhysicalDevice[] GetPhysicalDevices(Instance instance)
    {
        uint count = 0;

        vk.EnumeratePhysicalDevices(instance, &count, null);

        var properties = new PhysicalDevice[count];

        fixed (PhysicalDevice* ptr = properties)
        {
            vk.EnumeratePhysicalDevices(instance, &count, ptr);
        }

        return properties
            .Take((int)count)
            .ToArray();
    }

    static QueueFamilyProperties[] GetDeviceQueueProps(PhysicalDevice physicalDevice)
    {
        uint count = 0;

        vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &count, null);

        var properties = new QueueFamilyProperties[count];

        fixed (QueueFamilyProperties* ptr = properties)
        {
            vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &count, ptr);
        }

        return properties
            .Take((int)count)
            .ToArray();
    }

    static string[] GetDeviceExtensions(PhysicalDevice physicalDevice)
    {
        uint count = 0;

        vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &count, null);

        var properties = new ExtensionProperties[count];

        fixed (ExtensionProperties* ptr = properties)
        {
            vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, &count, ptr);
        }

        return properties
            .Take((int)count)
            .Select(x => SilkMarshal.PtrToString((nint)x.ExtensionName)!)
            .ToArray();
    }

    static string[] GetInstanceLayers()
    {
        uint count = 0;

        vk.EnumerateInstanceLayerProperties(&count, null);

        var properties = new LayerProperties[count];

        fixed (LayerProperties* ptr = properties)
        {
            vk.EnumerateInstanceLayerProperties(&count, ptr);
        }

        return properties
            .Take((int)count)
            .Select(x => SilkMarshal.PtrToString((nint)x.LayerName)!)
            .ToArray();
    }

    static string[] GetInstanceExtensions()
    {
        uint count = 0;

        vk.EnumerateInstanceExtensionProperties((byte*)0, &count, null);

        var properties = new ExtensionProperties[count];

        fixed (ExtensionProperties* ptr = properties)
        {
            vk.EnumerateInstanceExtensionProperties((byte*)0, &count, ptr);
        }

        return properties
            .Take((int)count)
            .Select(x => SilkMarshal.PtrToString((nint)x.ExtensionName)!)
            .ToArray();
    }

    static List<string> ByteToArray(byte** data, int count)
    {
        List<string> s = [];
        for (int i = 0; i < count; i++)
            s.Add(SilkMarshal.PtrToString((nint)data[i])!);
        return s;
    }
#endif
}