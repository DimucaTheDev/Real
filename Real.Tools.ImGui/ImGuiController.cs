using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Real.Windowing;
using Real.Graphics;
using Real.Graphics.Rhi;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums; // IDevice, IPipeline, IBuffer, IShader, ICommandBuffer, ITexture...

namespace Real.ImGui;

public sealed unsafe class ImGuiController : IDisposable
{
    private readonly IWindow _window;
    private readonly IGraphicsDevice _device;
    private readonly ImGuiInputHandler _input;
    private readonly ImGuiContextPtr _context;

    private IBuffer? _vertexBuffer;
    private IBuffer? _indexBuffer;
    private int _vertexBufferSize;
    private int _indexBufferSize;

    private ITexture? _fontTexture;   // TODO(RHI): нужен интерфейс текстуры + сэмплер
    private IShader _vertexShader;
    private IShader _fragmentShader;
    private IPipeline _pipeline;

    public ImGuiController(IWindow window, IDevice device, TextureFormat colorFormat)
    {
        _window = window;
        _device = device;

        _context = ImGui.CreateContext();
        ImGui.SetCurrentContext(_context);

        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        CreateDeviceResources(colorFormat);
        CreateFontsTexture();

        _input = new ImGuiInputHandler(window);
    }

    private void CreateDeviceResources(TextureFormat colorFormat)
    {
        // Шейдеры компилируются офлайн так же, как main.vert/main.frag —
        // положите GLSL ниже в shaders/imgui.vert / imgui.frag и прогоните
        // через ваш существующий шейдер-компилятор.
        _vertexShader = _device.CreateShader(ShaderStage.Vertex, File.ReadAllBytes("shaders/imgui.vert"));
        _fragmentShader = _device.CreateShader(ShaderStage.Fragment, File.ReadAllBytes("shaders/imgui.frag"));

        // Layout ImDrawVert: pos(vec2) + uv(vec2) + col(uint, rgba8)
        _pipeline = _device.CreatePipeline(new PipelineDescriptor
        {
            VertexShader = _vertexShader,
            FragmentShader = _fragmentShader,
            Topology = PrimitiveTopology.TriangleList,
            Blend = BlendState.AlphaBlend, // TODO(RHI): если такого пресета нет — обычный alpha-blend (src_alpha, one_minus_src_alpha)
            ColorAttachmentFormats = [colorFormat],
            DebugName = "ImGuiPipeline",
            DepthAttachmentFormat = null,
            DepthStencil = DepthStencilState.Disabled,
            Raster = new RasterState(CullMode.None), // важно: без culling, ImGui не гарантирует winding
            VertexLayout = new VertexLayout
            {
                Stride = (uint)sizeof(ImDrawVert),
                Attributes =
                [
                    new VertexAttribute(Location: 0, Format: TextureFormat.Rg32Float, Offset: 0),   // pos
                    new VertexAttribute(Location: 1, Format: TextureFormat.Rg32Float, Offset: 8),   // uv
                    new VertexAttribute(Location: 2, Format: TextureFormat.Rgba8Unorm, Offset: 16), // col
                ]
            }
        });
    }

    private unsafe void CreateFontsTexture()
    {
        var io = Hexa.NET.ImGui.ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int bpp);

        var data = new ReadOnlySpan<byte>(pixels, width * height * bpp);

        // TODO(RHI): подставьте реальный CreateTexture — нужен формат Rgba8Unorm,
        // usage Sampled, и способ его забиндить в фрагментном шейдере (sampler2D uTexture).
        _fontTexture = _device.CreateTexture(
            new TextureDescriptor(
                Width: (uint)width,
                Height: (uint)height,
                Format: TextureFormat.Rgba8Unorm,
                Usage: TextureUsage.Sampled,
                DebugName: "ImGuiFonts"),
            data);

        io.Fonts.SetTexID((ImTextureID)(nint)_fontTexture!.GetHandle()); // TODO(RHI): способ получить handle/ID текстуры
        io.Fonts.ClearTexData();
    }

    public void NewFrame(float deltaTime)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Vector2(_window.Size.Width, _window.Size.Height);
        io.DeltaTime = deltaTime > 0 ? deltaTime : 1f / 60f;

        ImGui.NewFrame();
    }

    public void Render(ICommandBuffer cmd)
    {
        ImGui.Render();
        var drawData = ImGui.GetDrawData();
        if (drawData.Handle == null || drawData.CmdListsCount == 0) return;

        EnsureBuffers(drawData);

        // Ортографическая проекция под текущий размер экрана
        float l = drawData.DisplayPos.X;
        float r = drawData.DisplayPos.X + drawData.DisplaySize.X;
        float t = drawData.DisplayPos.Y;
        float b = drawData.DisplayPos.Y + drawData.DisplaySize.Y;

        var ortho = Matrix4x4.CreateOrthographicOffCenter(l, r, b, t, -1f, 1f);

        cmd.BindPipeline(_pipeline);
        cmd.SetUniform("uProjection", in ortho, transpose: false);
        // TODO(RHI): cmd.BindTexture(0, _fontTexture) — если у вас есть привязка текстур/дескрипторов

        int vtxOffset = 0, idxOffset = 0;

        var clipOff = drawData.DisplayPos;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists.Data[n];

            // TODO(RHI): апдейт содержимого буфера с CPU (map/upload) —
            // здесь предполагается некий cmd.UpdateBuffer / _vertexBuffer.Write(...)
            UploadVertices(cmdList, vtxOffset);
            UploadIndices(cmdList, idxOffset);

            cmd.BindVertexBuffer(_vertexBuffer!);
            cmd.BindIndexBuffer(_indexBuffer!); // TODO(RHI): если индексного байндинга нет — добавить

            for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
            {
                var pcmd = cmdList.CmdBuffer.Data[i];
                if (pcmd.UserCallback != null)
                {
                    continue; // редкий случай, обычно не используется
                }

                var clipMin = new Vector2(pcmd.ClipRect.X - clipOff.X, pcmd.ClipRect.Y - clipOff.Y);
                var clipMax = new Vector2(pcmd.ClipRect.Z - clipOff.X, pcmd.ClipRect.W - clipOff.Y);
                if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y) continue;

                cmd.SetScissor(
                    (int)clipMin.X, (int)clipMin.Y,
                    (uint)(clipMax.X - clipMin.X), (uint)(clipMax.Y - clipMin.Y));

                // TODO(RHI): если поддерживаете несколько текстур (pcmd.TextureId) —
                // здесь нужно перебиндить нужную текстуру перед Draw.

                // TODO(RHI): нужен индексированный draw с offset'ами:
                cmd.DrawIndexed(
                    indexCount: (int)pcmd.ElemCount,
                    indexOffset: idxOffset + (int)pcmd.IdxOffset,
                    vertexOffset: vtxOffset + (int)pcmd.VtxOffset);
            }

            vtxOffset += cmdList.VtxBuffer.Size;
            idxOffset += cmdList.IdxBuffer.Size;
        }
    }

    private void EnsureBuffers(ImDrawDataPtr drawData)
    {
        int vtxSize = drawData.TotalVtxCount * sizeof(ImDrawVert);
        int idxSize = drawData.TotalIdxCount * sizeof(ushort);

        if (_vertexBuffer == null || vtxSize > _vertexBufferSize)
        {
            _vertexBufferSize = (int)(vtxSize * 1.5f) + 1;
            _vertexBuffer?.Dispose(); // если IBuffer одноразовый
            _vertexBuffer = _device.CreateBuffer(
                new BufferDescriptor(
                    Size: (ulong)_vertexBufferSize,
                    Usage: BufferUsage.Vertex, // TODO(RHI): нужен ещё флаг "Dynamic/CpuWritable"
                    DebugName: "ImGuiVertices"),
                ReadOnlySpan<byte>.Empty);
        }

        if (_indexBuffer == null || idxSize > _indexBufferSize)
        {
            _indexBufferSize = (int)(idxSize * 1.5f) + 1;
            _indexBuffer?.Dispose();
            _indexBuffer = _device.CreateBuffer(
                new BufferDescriptor(
                    Size: (ulong)_indexBufferSize,
                    Usage: BufferUsage.Index,
                    DebugName: "ImGuiIndices"),
                ReadOnlySpan<byte>.Empty);
        }
    }

    private void UploadVertices(ImDrawListPtr cmdList, int vtxOffset)
    {
        var span = new ReadOnlySpan<byte>(
            cmdList.VtxBuffer.Data,
            cmdList.VtxBuffer.Size * sizeof(ImDrawVert));

        // TODO(RHI): у вас должен быть способ записать данные в уже созданный буфер,
        // например _vertexBuffer.Update(offsetBytes, span) или через staging buffer.
        _vertexBuffer!.Update((ulong)(vtxOffset * sizeof(ImDrawVert)), span);
    }

    private void UploadIndices(ImDrawListPtr cmdList, int idxOffset)
    {
        var span = new ReadOnlySpan<byte>(
            cmdList.IdxBuffer.Data,
            cmdList.IdxBuffer.Size * sizeof(ushort));

        _indexBuffer!.Update((ulong)(idxOffset * sizeof(ushort)), span);
    }

    public void Dispose()
    {
        _input.Dispose();

        _device.DestroyPipeline(_pipeline);
        _device.DestroyShader(_vertexShader);
        _device.DestroyShader(_fragmentShader);

        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _fontTexture?.Dispose();

        ImGui.DestroyContext(_context);
    }
}