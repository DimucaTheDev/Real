using System.Diagnostics;
using System.Numerics;
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

    /*
     * ВАЖНО:
     *
     * Мы используем RGBA32Float для обеих атрибутов,
     * поэтому Position = 16 байт и Color = 16 байт.
     *
     * Это специально сделано так, чтобы VertexLayout
     * точно соответствовал реальному содержимому буфера.
     */
    private struct Vertex
    {
        public float X, Y, Z, W;
        public float R, G, B, A;

        public Vertex(
            float x, float y, float z,
            float r, float g, float b)
        {
            X = x;
            Y = y;
            Z = z;
            W = 1.0f;

            R = r;
            G = g;
            B = b;
            A = 1.0f;
        }
    }

    private struct FaceInfo
    {
        public int Index;
        public Vector3 Center;
        public float Depth;
    }

    static void Main(string[] args)
    {
        var api = Enum.Parse<GraphicsApi>(
            args.FirstOrDefault("vulkan")!,
            true);

        window = new GlfwWindow(api);

        IGraphicsBackendFactory factory = api switch
        {
            GraphicsApi.OpenGl => new OpenGlBackendFactory(),
            GraphicsApi.Vulkan => new VulkanBackendFactory(),
            _ => throw new NotSupportedException()
        };

        using var device = factory.CreateDevice(
            window,
            Debugger.IsAttached);

        using var swapchain = device.CreateSwapchain(window);

        /*
         * Каждая грань находится в отдельном буфере.
         *
         * Это позволяет нам при отсутствии depth-buffer
         * рисовать грани в правильном порядке:
         * far -> near.
         */

        var front =
            new Vertex[]
            {
                new(-0.5f, -0.5f,  0.5f, 0.95f, 0.22f, 0.22f),
                new( 0.5f, -0.5f,  0.5f, 0.95f, 0.22f, 0.22f),
                new( 0.5f,  0.5f,  0.5f, 0.95f, 0.22f, 0.22f),

                new(-0.5f, -0.5f,  0.5f, 0.95f, 0.22f, 0.22f),
                new( 0.5f,  0.5f,  0.5f, 0.95f, 0.22f, 0.22f),
                new(-0.5f,  0.5f,  0.5f, 0.95f, 0.22f, 0.22f),
            };

        var back =
            new Vertex[]
            {
                new( 0.5f, -0.5f, -0.5f, 0.05f, 0.85f, 0.85f),
                new(-0.5f, -0.5f, -0.5f, 0.05f, 0.85f, 0.85f),
                new(-0.5f,  0.5f, -0.5f, 0.05f, 0.85f, 0.85f),

                new( 0.5f, -0.5f, -0.5f, 0.05f, 0.85f, 0.85f),
                new(-0.5f,  0.5f, -0.5f, 0.05f, 0.85f, 0.85f),
                new( 0.5f,  0.5f, -0.5f, 0.05f, 0.85f, 0.85f),
            };

        var top =
            new Vertex[]
            {
                new(-0.5f,  0.5f,  0.5f, 0.15f, 0.85f, 0.35f),
                new( 0.5f,  0.5f,  0.5f, 0.15f, 0.85f, 0.35f),
                new( 0.5f,  0.5f, -0.5f, 0.15f, 0.85f, 0.35f),

                new(-0.5f,  0.5f,  0.5f, 0.15f, 0.85f, 0.35f),
                new( 0.5f,  0.5f, -0.5f, 0.15f, 0.85f, 0.35f),
                new(-0.5f,  0.5f, -0.5f, 0.15f, 0.85f, 0.35f),
            };

        var bottom =
            new Vertex[]
            {
                new(-0.5f, -0.5f, -0.5f, 0.20f, 0.40f, 0.95f),
                new( 0.5f, -0.5f, -0.5f, 0.20f, 0.40f, 0.95f),
                new( 0.5f, -0.5f,  0.5f, 0.20f, 0.40f, 0.95f),

                new(-0.5f, -0.5f, -0.5f, 0.20f, 0.40f, 0.95f),
                new( 0.5f, -0.5f,  0.5f, 0.20f, 0.40f, 0.95f),
                new(-0.5f, -0.5f,  0.5f, 0.20f, 0.40f, 0.95f),
            };

        var right =
            new Vertex[]
            {
                new(0.5f, -0.5f,  0.5f, 0.95f, 0.70f, 0.10f),
                new(0.5f, -0.5f, -0.5f, 0.95f, 0.70f, 0.10f),
                new(0.5f,  0.5f, -0.5f, 0.95f, 0.70f, 0.10f),

                new(0.5f, -0.5f,  0.5f, 0.95f, 0.70f, 0.10f),
                new(0.5f,  0.5f, -0.5f, 0.95f, 0.70f, 0.10f),
                new(0.5f,  0.5f,  0.5f, 0.95f, 0.70f, 0.10f),
            };

        var left =
            new Vertex[]
            {
                new(-0.5f, -0.5f, -0.5f, 0.75f, 0.20f, 0.85f),
                new(-0.5f, -0.5f,  0.5f, 0.75f, 0.20f, 0.85f),
                new(-0.5f,  0.5f,  0.5f, 0.75f, 0.20f, 0.85f),

                new(-0.5f, -0.5f, -0.5f, 0.75f, 0.20f, 0.85f),
                new(-0.5f,  0.5f,  0.5f, 0.75f, 0.20f, 0.85f),
                new(-0.5f,  0.5f, -0.5f, 0.75f, 0.20f, 0.85f),
            };

        /*
         * Центры граней.
         *
         * Они нужны только для painter's algorithm:
         * определяем, какая грань дальше от камеры.
         */
        Vector3[] faceCenters =
        [
            new( 0.0f,  0.0f,  0.5f), // Front
            new( 0.0f,  0.0f, -0.5f), // Back
            new( 0.0f,  0.5f,  0.0f), // Top
            new( 0.0f, -0.5f,  0.0f), // Bottom
            new( 0.5f,  0.0f,  0.0f), // Right
            new(-0.5f,  0.0f,  0.0f), // Left
        ];

        var faceBuffers = new
        {
            Front = device.CreateBuffer(
                new BufferDescriptor(
                    Size: (ulong)(front.Length * Marshal.SizeOf<Vertex>()),
                    Usage: BufferUsage.Vertex,
                    DebugName: "CubeFront"),
                MemoryMarshal.AsBytes(front.AsSpan())),

            Back = device.CreateBuffer(
                new BufferDescriptor(
                    Size: (ulong)(back.Length * Marshal.SizeOf<Vertex>()),
                    Usage: BufferUsage.Vertex,
                    DebugName: "CubeBack"),
                MemoryMarshal.AsBytes(back.AsSpan())),

            Top = device.CreateBuffer(
                new BufferDescriptor(
                    Size: (ulong)(top.Length * Marshal.SizeOf<Vertex>()),
                    Usage: BufferUsage.Vertex,
                    DebugName: "CubeTop"),
                MemoryMarshal.AsBytes(top.AsSpan())),

            Bottom = device.CreateBuffer(
                new BufferDescriptor(
                    Size: (ulong)(bottom.Length * Marshal.SizeOf<Vertex>()),
                    Usage: BufferUsage.Vertex,
                    DebugName: "CubeBottom"),
                MemoryMarshal.AsBytes(bottom.AsSpan())),

            Right = device.CreateBuffer(
                new BufferDescriptor(
                    Size: (ulong)(right.Length * Marshal.SizeOf<Vertex>()),
                    Usage: BufferUsage.Vertex,
                    DebugName: "CubeRight"),
                MemoryMarshal.AsBytes(right.AsSpan())),

            Left = device.CreateBuffer(
                new BufferDescriptor(
                    Size: (ulong)(left.Length * Marshal.SizeOf<Vertex>()),
                    Usage: BufferUsage.Vertex,
                    DebugName: "CubeLeft"),
                MemoryMarshal.AsBytes(left.AsSpan())),
        };

        var vertexShader =
            device.CreateShader(
                ShaderStage.Vertex,
                File.ReadAllBytes("shaders/main.vert"));

        var fragmentShader =
            device.CreateShader(
                ShaderStage.Fragment,
                File.ReadAllBytes("shaders/main.frag"));

        /*
         * Vertex:
         *
         * Position:
         *   float4 = 16 bytes
         *
         * Color:
         *   float4 = 16 bytes
         *
         * Итого:
         *   32 bytes
         */
        var pipeline =
            device.CreatePipeline(
                new PipelineDescriptor()
                {
                    VertexShader = vertexShader,
                    FragmentShader = fragmentShader,

                    Topology = PrimitiveTopology.TriangleList,

                    Blend = BlendState.Opaque,

                    ColorAttachmentFormats =
                    [
                        swapchain.Format
                    ],

                    DebugName = "CubePipeline",

                    /*
                     * Пока намеренно без depth buffer.
                     *
                     * Порядок граней регулируется на CPU.
                     */
                    DepthAttachmentFormat = null,
                    DepthStencil = DepthStencilState.Disabled,

                    /*
                     * Куб выпуклый, а все 6 граней имеют согласованный CCW-winding
                     * (проверено векторным произведением для каждой грани), поэтому
                     * обычный backface culling корректно скрывает невидимые грани
                     * для выпуклого тела сам по себе, без depth-buffer'а.
                     * painter's algorithm на CPU (сортировка по центру грани) даёт сбой
                     * именно на ракурсах/рёбрах, когда несколько граней
                     * перекрываются на экране одновременно — именно это давало
                     * "ломанную плоскость" вместо куба.
                     */
                    Raster = new RasterState(CullMode.Back),

                    VertexLayout = new VertexLayout()
                    {
                        Stride = (uint)Marshal.SizeOf<Vertex>(),

                        Attributes =
                        [
                            new VertexAttribute(
                                Location: 0,
                                Format: TextureFormat.Rgba32Float,
                                Offset: 0),

                            new VertexAttribute(
                                Location: 1,
                                Format: TextureFormat.Rgba32Float,
                                Offset: 16),
                        ]
                    }
                });

        var stopwatch = Stopwatch.StartNew();

        var faces = new FaceInfo[6];
        while (!window.IsClosing)
        {
            window.PollEvents();

            if (window.Size.Width <= 0 ||
                window.Size.Height <= 0)
            {
                Thread.Sleep(16);
                continue;
            }

            var backbuffer =
                swapchain.AcquireNextImage();

            if (!backbuffer.IsValid)
            {
                Thread.Sleep(16);
                continue;
            }

            float time =
                (float)stopwatch.Elapsed.TotalSeconds;

            float aspect =
                (float)window.Size.Width /
                Math.Max(1f, window.Size.Height);

            var model =
                Matrix4x4.CreateRotationX(time * 0.75f) *
                Matrix4x4.CreateRotationY(time * 1.10f) *
                Matrix4x4.CreateRotationZ(time * 0.35f);

            var view =
                Matrix4x4.CreateLookAt(
                    new Vector3(0, 0, 3.5f),
                    Vector3.Zero,
                    Vector3.UnitY);

            var projection =
                Matrix4x4.CreatePerspectiveFieldOfView(
                    MathF.PI / 4.0f,
                    aspect,
                    0.1f,
                    100.0f);

            if (api == GraphicsApi.Vulkan)
            {
                projection.M22 = -projection.M22;
            }

            var mvp = model * view * projection;

            /*
             * Для painter's algorithm нам нужна глубина
             * центров граней относительно камеры.
             */
            for (int i = 0; i < 6; i++)
            {
                var worldCenter =
                    Vector3.Transform(
                        faceCenters[i],
                        model);

                var viewCenter =
                    Vector3.Transform(
                        worldCenter,
                        view);

                faces[i] = new FaceInfo
                {
                    Index = i,
                    Center = worldCenter,
                    Depth = viewCenter.Z
                };
            }

            /*
             * Сначала дальние грани,
             * потом ближние.
             *
             * Для нашей камеры дальние имеют
             * меньшее (более отрицательное) Z.
             */
            Array.Sort(
                faces,
                static (a, b) =>
                    a.Depth.CompareTo(b.Depth));

            var graph =
                device.CreateRenderGraph();

            graph
                .AddPass("CubePass")
                .Writes(backbuffer)
                .SetExecute(cmd =>
                {
                    cmd.SetViewport(
                        0,
                        0,
                        window.Size.Width,
                        window.Size.Height);

                    cmd.SetScissor(
                        0,
                        0,
                        (uint)window.Size.Width,
                        (uint)window.Size.Height);

                    cmd.BindPipeline(pipeline);

                    /*
                     * ВАЖНО: ЗДЕСЬ НЕ НУЖНО транспонировать матрицу вручную.
                     *
                     * System.Numerics.Matrix4x4 хранится row-major и используется
                     * с row-vector конвенцией (v' = v * M), а GLSL/SPIR-V ожидает
                     * column-major данные для column-vector конвенции (v' = M * v).
                     *
                     * Если просто отправить сырую память row-major(mvp) и сказать GPU
                     * "это уже column-major, не транспонируй" (transpose: false), то GPU прочитает
                     * эти же числа по столбцам вместо строк — а это математически и есть
                     * транспонирование. То есть дополнительно вызывать
                     * Matrix4x4.Transpose(mvp) перед этим НЕ НАДО — это давало двойной
                     * эффект (транспонирование в коде + "транспонирование" при чтении
                     * колонками), что в итоге отправляло на GPU зеркальный mvp вместо
                     * его транспонированной формы — отсюда и был перекошенный/
                     * "sheared" куб.
                     */
                    cmd.SetUniform(
                        "uMVP",
                        in mvp,
                        transpose: false);

                    /*
                     * Рисуем грани в отсортированном порядке.
                     */
                    for (int i = 0; i < 6; i++)
                    {
                        switch (faces[i].Index)
                        {
                            case 0:
                                cmd.BindVertexBuffer(
                                    faceBuffers.Front);
                                break;

                            case 1:
                                cmd.BindVertexBuffer(
                                    faceBuffers.Back);
                                break;

                            case 2:
                                cmd.BindVertexBuffer(
                                    faceBuffers.Top);
                                break;

                            case 3:
                                cmd.BindVertexBuffer(
                                    faceBuffers.Bottom);
                                break;

                            case 4:
                                cmd.BindVertexBuffer(
                                    faceBuffers.Right);
                                break;

                            case 5:
                                cmd.BindVertexBuffer(
                                    faceBuffers.Left);
                                break;

                            default:
                                throw new UnreachableException();
                        }

                        cmd.Draw(vertexCount: 6);
                    }
                });

            graph.Execute();

            swapchain.Present();
        }

        device.WaitIdle();

        device.DestroyPipeline(pipeline);

        device.DestroyShader(vertexShader);
        device.DestroyShader(fragmentShader);

        device.DestroyBuffer(faceBuffers.Front);
        device.DestroyBuffer(faceBuffers.Back);
        device.DestroyBuffer(faceBuffers.Top);
        device.DestroyBuffer(faceBuffers.Bottom);
        device.DestroyBuffer(faceBuffers.Right);
        device.DestroyBuffer(faceBuffers.Left);
    }
}