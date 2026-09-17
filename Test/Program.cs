using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Real;
using Real.Graphics.OpenGL;
using Real.Graphics.Rhi;
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

    static void Main(string[] args)
    { 
        var api = Enum.Parse<GraphicsApi>(args.FirstOrDefault("vulkan")!, true);
        window = new GlfwWindow(api);
        IGraphicsBackendFactory factory = api switch
        {
            GraphicsApi.OpenGl => new OpenGlBackendFactory(),
            GraphicsApi.Vulkan => new VulkanBackendFactory(),
            _ => throw new()
        };
        using var device = factory.CreateDevice(window, Debugger.IsAttached);
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
        SoundTest.Test();
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
}