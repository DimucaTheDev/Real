using System.Numerics;
using Hexa.NET.ImGui;
using Real.Graphics.Rhi;
using Real.Graphics.Rhi.Descriptors;
using Real.Graphics.Rhi.Enums;
using Real.Graphics.Rhi.Handles;
using Real.Windowing;

namespace Real.ImGui;

/// <summary>
/// Рендер-бэкенд ImGui (Hexa.NET.ImGui 2.2.9, собран на Dear ImGui 1.92+) поверх
/// Real.Graphics.Rhi.
///
/// ВАЖНО ПРО ВЕРСИЮ ImGui: начиная с 1.92 (июнь 2025) старый протокол шрифтов -
/// io.Fonts.GetTexDataAsRGBA32() / SetTexID() / Build() / IsBuilt() - ОБЪЯВЛЕН
/// УСТАРЕВШИМ, а в сборке Hexa.NET.ImGui эти функции ВООБЩЕ ВЫПИЛЕНЫ из
/// биндинга (не просто deprecated - их нет в скомпилированном cimgui).
/// Вместо этого используется новый протокол текстур: бэкенд выставляет
/// io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures, а каждый кадр
/// проходит по drawData.Textures[] и обрабатывает переходы состояния
/// (WantCreate -> Ok, WantUpdates -> Ok, WantDestroy -> Destroyed) для КАЖДОЙ
/// текстуры, включая атлас шрифта - он теперь просто одна из записей в этом
/// списке, а не что-то, что нужно доставать вручную при старте. Смотри
/// официальный docs/BACKENDS.md (раздел "Adding support for
/// ImGuiBackendFlags_RendererHasTextures") и пример
/// Examples/ExampleMonoGame/ImGuiRenderer.cs из репозитория HexaEngine/Hexa.NET.ImGui -
/// код ниже следует ровно этому паттерну.
///
/// ДРУГИЕ ОГРАНИЧЕНИЯ ТЕКУЩЕГО RHI (Real.Graphics.Rhi), под которые эта
/// реализация подстроена:
///
/// 1) НЕТ СПОСОБА ОБНОВИТЬ СОДЕРЖИМОЕ УЖЕ СОЗДАННОГО БУФЕРА ИЛИ ТЕКСТУРЫ.
///    IGraphicsDevice.CreateBuffer/CreateTexture(descriptor, initialData) -
///    единственный способ положить данные в ресурс, апдейта после создания
///    нет. Поэтому:
///    - вершинный и индексный буферы ImGui пересоздаются целиком КАЖДЫЙ КАДР;
///    - WantUpdates для текстуры тоже обрабатывается как полное
///      уничтожение + пересоздание текстуры (частичный апдейт через
///      textureData.Updates[]/UpdateRect не поддержать, пока в RHI нет
///      метода записи в существующую текстуру).
///    На Vulkan это особенно дорого: VulkanBufferPool.Create/VulkanTexturePool.Create
///    с initialData делают блокирующий staging-upload (QueueSubmit +
///    QueueWaitIdle) - т.е. каждый кадр минимум одна полная синхронизация
///    CPU/GPU только ради вершин/индексов ImGui, и ещё одна на каждое
///    создание/пересоздание текстуры. Комментарии в самом VulkanBufferPool
///    прямо говорят: "Blocking wait: acceptable for load-time uploads, not
///    for per-frame streaming". Если нужен нормальный фреймрейт - в
///    IGraphicsDevice нужен настоящий UpdateBuffer/UpdateTexture для
///    CpuVisible-ресурсов.
///
/// 2) ИНДЕКСЫ ВСЕГДА 32-БИТНЫЕ. OpenGlCommandList.DrawIndexed жёстко
///    использует DrawElementsType.UnsignedInt, а VulkanCommandList.BindIndexBuffer -
///    IndexType.Uint32. Дефолтный ImDrawIdx в ImGui - unsigned short (16 бит),
///    поэтому индексы конвертируются на CPU из ushort в uint перед загрузкой.
///
/// 3) ТОЛЬКО Rgba32Float НАДЁЖНО РАБОТАЕТ КАК ФОРМАТ ВЕРШИННОГО АТРИБУТА.
///    См. GlFormatMap.ToVertexAttrib и VkFormatMap.ToVkVertexFormat -
///    Rgba8Unorm/Rg8Unorm как вершинные атрибуты трактуются как float2/float3
///    (а не как честные упакованные байты) и по-разному на двух бэкендах.
///    Поэтому вершина ImGui расширена: pos/uv/color - все vec4 float
///    (48 байт вместо родных 20). См. также комментарий в imgui.vert.
///
/// 4) Font-текстура (и вообще любая текстура, если понадобится ImGui.Image())
///    требует SetTexture, а тот на Vulkan работает, только если у пайплайна
///    есть непустой descriptor set layout - раньше VulkanPipelinePool.Create
///    создавал layout с BindingCount = 0 для вообще любого пайплайна
///    (SetTexture был гарантированно сломан на Vulkan). Это исправлено в
///    VulkanPipelinePool - добавлен binding 0 (combined image sampler) и
///    binding 1 (uniform buffer).
/// </summary>
public sealed unsafe class ImGuiController : IDisposable
{
    // Вершина, раздутая под единственный надёжно работающий формат
    // атрибута в текущем RHI (см. class-level комментарий, пункт 3).
    private struct ImGuiVertex
    {
        public Vector4 Position; // .xy используется
        public Vector4 Uv;       // .xy используется
        public Vector4 Color;    // rgba, уже 0..1 float
    }

    private readonly IWindow _window;
    private readonly IGraphicsDevice _device;
    private readonly ImGuiInputHandler _input;
    private readonly ImGuiContextPtr _context;

    private readonly ShaderHandle _vertexShader;
    private readonly ShaderHandle _fragmentShader;
    private readonly PipelineHandle _pipeline;

    // Общий сэмплер на все текстуры ImGui (шрифт + всё, что пользователь
    // передаст через ImGui.Image()). Отдельного сэмплера на текстуру RHI
    // всё равно не просит - SetTexture берёт сэмплер отдельным параметром.
    private readonly SamplerHandle _sampler;

    // Наши RHI-хендлы текстур, привязанные к ImTextureID, который МЫ САМИ
    // назначаем и явно кладём через SetTexID() при обработке WantCreate -
    // в отличие от того, что предполагал официальный пример ExampleMonoGame
    // (там ImTextureID читается без предварительного SetTexID), рантайм
    // Hexa.NET.ImGui 2.2.9 реально ассертит:
    // "ImDrawCmd is referring to ImTextureData that wasn't uploaded to
    // graphics system. Backend must call ImTextureData::SetTexID() after
    // handling ImTextureStatus_WantCreate request!" - то есть TexID НЕ
    // назначается ядром автоматически, это обязанность бэкенда.
    private readonly Dictionary<ImTextureID, TextureHandle> _textures = new();

    // 0 зарезервирован под ImTextureID.Null (см. ImTextureID.IsNull), а
    // ассерт выше буквально проверяет tex_id != 0 - поэтому начинаем с 1.
    private ulong _nextTextureId = 1;

    // Буферы предыдущего кадра, которые нужно уничтожить в начале следующего
    // (см. пункт 1 в комментарии класса про отсутствие Update-API).
    private BufferHandle? _vertexBuffer;
    private BufferHandle? _indexBuffer;

    private ImGuiVertex[] _vertexScratch = new ImGuiVertex[4096];
    private uint[] _indexScratch = new uint[8192];

    public ImGuiController(IWindow window, IGraphicsDevice device, TextureFormat colorFormat)
    {
        _window = window;
        _device = device;

        _context = Hexa.NET.ImGui.ImGui.CreateContext();
        Hexa.NET.ImGui.ImGui.SetCurrentContext(_context);

        var io = Hexa.NET.ImGui.ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;

        // Новый протокол текстур (см. class-level комментарий) - без этого
        // флага ImGui на 1.92+ падает в NewFrame() с "font atlas is not built",
        // т.к. старый прогрев атласа через GetTexDataAsRGBA32 в этой сборке
        // недоступен вообще.
        io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures;

        // ВАЖНО: не выставляем ImGuiBackendFlags.RendererHasVtxOffset - у
        // ICommandList.DrawIndexed нет параметра base vertex, поэтому мы
        // полагаемся на то, что без этого флага ImGui сам держит индексы
        // каждого ImDrawList абсолютными относительно начала ЕГО вершинного
        // буфера (VtxOffset в командах всегда 0), а нужный кусок общего
        // вершинного буфера мы подставляем через offset в BindVertexBuffer.

        (_vertexShader, _fragmentShader, _pipeline) = CreateDeviceResources(colorFormat);

        _sampler = _device.CreateSampler(new SamplerDescriptor(
            MinFilter: FilterMode.Linear,
            MagFilter: FilterMode.Linear,
            AddressU: AddressMode.ClampToEdge,
            AddressV: AddressMode.ClampToEdge));

        _input = new ImGuiInputHandler(window);
    }

    private (ShaderHandle, ShaderHandle, PipelineHandle) CreateDeviceResources(TextureFormat colorFormat)
    {
        var baseDir = AppContext.BaseDirectory;
        byte[] vsSource = File.ReadAllBytes(Path.Combine(baseDir, "shaders", "imgui.vert"));
        byte[] fsSource = File.ReadAllBytes(Path.Combine(baseDir, "shaders", "imgui.frag"));

        var vertexShader = _device.CreateShader(ShaderStage.Vertex, vsSource);
        var fragmentShader = _device.CreateShader(ShaderStage.Fragment, fsSource);

        var pipeline = _device.CreatePipeline(new PipelineDescriptor(
            VertexShader: vertexShader,
            FragmentShader: fragmentShader,
            VertexLayout: new VertexLayout
            {
                Stride = (uint)sizeof(ImGuiVertex),
                Attributes =
                [
                    new VertexAttribute(Location: 0, Format: TextureFormat.Rgba32Float, Offset: 0),  // position
                    new VertexAttribute(Location: 1, Format: TextureFormat.Rgba32Float, Offset: 16), // uv
                    new VertexAttribute(Location: 2, Format: TextureFormat.Rgba32Float, Offset: 32), // color
                ]
            },
            Topology: PrimitiveTopology.TriangleList,
            Blend: BlendState.AlphaBlend,
            DepthStencil: DepthStencilState.Disabled,
            Raster: new RasterState(CullMode.None), // ImGui не гарантирует winding
            ColorAttachmentFormats: [colorFormat],
            DepthAttachmentFormat: null,
            DebugName: "ImGuiPipeline"));

        return (vertexShader, fragmentShader, pipeline);
    }

    public void NewFrame(float deltaTime)
    {
        var io = Hexa.NET.ImGui.ImGui.GetIO();
        io.DisplaySize = new Vector2(_window.Size.Width, _window.Size.Height);
        io.DeltaTime = deltaTime > 0f ? deltaTime : 1f / 60f;

        Hexa.NET.ImGui.ImGui.NewFrame();
    }

    public void Render(ICommandList cmd)
    {
        Hexa.NET.ImGui.ImGui.Render();
        var drawData = Hexa.NET.ImGui.ImGui.GetDrawData();
        if (drawData.Handle == null || drawData.CmdListsCount == 0)
            return;

        // Обязательный шаг протокола RendererHasTextures - см. class-level
        // комментарий. Делается ДО чтения drawData.Textures.Size (у пустого
        // ImVector .Data == null - см. пример HexaEngine/Hexa.NET.ImGui).
        ProcessTextureUpdates(drawData);

        // Пункт 1 из class-level комментария: буферы прошлого кадра больше
        // не нужны - удаляем перед тем, как создать новые. Строго говоря,
        // если GPU ещё не закончил читать прошлый кадр (несколько кадров в
        // полёте), это преждевременное удаление - тот же риск, что и везде
        // в этом RHI (см. TODO в VulkanBufferPool.Destroy про deferred-delete
        // очередь).
        DestroyFrameBuffers();

        BuildBuffers(drawData, out var vtxBuffer, out var idxBuffer);
        _vertexBuffer = vtxBuffer;
        _indexBuffer = idxBuffer;

        float l = drawData.DisplayPos.X;
        float r = drawData.DisplayPos.X + drawData.DisplaySize.X;
        float t = drawData.DisplayPos.Y;
        float b = drawData.DisplayPos.Y + drawData.DisplaySize.Y;

        // ВАЖНО: bottom/top здесь НАМЕРЕННО переставлены местами (t как bottom,
        // b как top), а не "как логично было бы" (b как bottom, t как top).
        //
        // Причина - OpenGlDevice вызывает _gl.ClipControl(ClipControlOrigin.UpperLeft, ...).
        // Формула glClipControl (см. спецификацию ARB_clip_control): y_ndc = f * y_clip / w,
        // где f = -1 для UPPER_LEFT (а не просто перенумерация gl_FragCoord - это
        // настоящая инверсия NDC по Y). Тот же самый баг с тем же самым фиксом
        // разбирался в самом Dear ImGui: https://github.com/ocornut/imgui/issues/3143 -
        // "When using GL_UPPER_LEFT origin, invert the y coordinate of the projection
        // matrix". Без этой перестановки экран физически-верхних пикселей ImGui
        // попадает в ndc=+1, что после ClipControl-инверсии (f=-1) даёt ndc_d=-1,
        // а это, в свою очередь, через НЕИЗМЕННУЮ (ClipControl её не трогает)
        // адресацию glViewport - это низ окна. Итог - весь UI вверх ногами.
        //
        // На Vulkan эта же перестановка ТОЖЕ обязательна (независимо от GL!) - у
        // Vulkan своя, отдельная причина: его framebuffer/viewport адресация
        // нативно top-left-origin (ndc=+1 маппится на низ вьюпорта), в отличие
        // от cube-пайплайна в Program.cs, который компенсирует это через
        // projection.M22 = -projection.M22 только для Vulkan. Здесь я не стал
        // заводить такое же ветвление по API - перестановка t/b ниже даёт
        // корректный результат сразу на обоих бэкендах одной и той же матрицей
        // (проверено алгебраически по формулам обеих API, не по факту "похоже
        // работает" - см. историю обсуждения).
        var ortho = Matrix4x4.CreateOrthographicOffCenter(l, r, t, b, -1f, 1f);

        cmd.BindPipeline(_pipeline);
        cmd.SetUniform("uMVP", in ortho, transpose: false);

        var clipOff = drawData.DisplayPos;
        int vtxOffset = 0, idxOffset = 0;

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists.Data[n];

            cmd.BindVertexBuffer(vtxBuffer, slot: 0, offset: (ulong)(vtxOffset * sizeof(ImGuiVertex)));
            cmd.BindIndexBuffer(idxBuffer, offset: (ulong)(idxOffset * sizeof(uint)));

            for (int i = 0; i < cmdList.CmdBuffer.Size; i++)
            {
                var pcmd = cmdList.CmdBuffer.Data[i];
                if (pcmd.UserCallback != null)
                    continue;

                var clipMin = new Vector2(pcmd.ClipRect.X - clipOff.X, pcmd.ClipRect.Y - clipOff.Y);
                var clipMax = new Vector2(pcmd.ClipRect.Z - clipOff.X, pcmd.ClipRect.W - clipOff.Y);
                if (clipMax.X <= clipMin.X || clipMax.Y <= clipMin.Y)
                    continue;

                // ImDrawCmd.TextureId в этой версии не существует - текстура
                // теперь ImTextureRef (TexRef), а ImTextureID достаётся через
                // GetTexID() (== TexRef.TexData ? TexRef.TexData->TexID : TexRef.TexID).
                var textureId = pcmd.GetTexID();
                if (!_textures.TryGetValue(textureId, out var textureHandle))
                    continue; // текстура ещё не создана/уже уничтожена - пропускаем draw

                cmd.SetScissor(
                    (int)clipMin.X, (int)clipMin.Y,
                    (uint)(clipMax.X - clipMin.X), (uint)(clipMax.Y - clipMin.Y));

                cmd.SetTexture(0, textureHandle, _sampler);

                // VtxOffset здесь всегда 0 - см. комментарий про
                // RendererHasVtxOffset в конструкторе. firstIndex - это
                // смещение ВНУТРИ уже забинженного (через offset выше)
                // куска индексного буфера для этого ImDrawList.
                cmd.DrawIndexed(
                    indexCount: pcmd.ElemCount,
                    instanceCount: 1,
                    firstIndex: pcmd.IdxOffset);
            }

            vtxOffset += cmdList.VtxBuffer.Size;
            idxOffset += cmdList.IdxBuffer.Size;
        }
    }

    /// <summary>
    /// Новый протокол текстур ImGui 1.92+ (см. class-level комментарий).
    /// Паттерн 1-в-1 повторяет Examples/ExampleMonoGame/ImGuiRenderer.cs из
    /// репозитория HexaEngine/Hexa.NET.ImGui, только вместо Texture2D/MonoGame
    /// создаются TextureHandle через IGraphicsDevice.
    /// </summary>
    private void ProcessTextureUpdates(ImDrawDataPtr drawData)
    {
        if (drawData.Textures.Data == null)
            return;

        for (int i = 0; i < drawData.Textures.Size; i++)
        {
            var textureData = drawData.Textures.Data[i];
            switch (textureData.Status)
            {
                case ImTextureStatus.WantCreate:
                    CreateTexture(textureData);
                    break;

                case ImTextureStatus.WantUpdates:
                    // В IGraphicsDevice нет метода записи в существующую
                    // текстуру (см. пункт 1 class-level комментария) - честный
                    // частичный апдейт через textureData.Updates[]/UpdateRect
                    // тут сделать нельзя. Пересоздаём текстуру целиком - как
                    // и MonoGame-пример в самом Hexa.NET.ImGui, который в этом
                    // месте тоже прямо помечен TODO и тоже просто копирует всё.
                    DestroyTexture(textureData, markDestroyed: false);
                    CreateTexture(textureData);
                    break;

                case ImTextureStatus.WantDestroy:
                    // BACKENDS.md явно требует ждать UnusedFrames > 0 перед
                    // реальным уничтожением (GPU может ещё дорисовывать кадр,
                    // где эта текстура использовалась).
                    if (textureData.UnusedFrames > 0)
                        DestroyTexture(textureData, markDestroyed: true);
                    break;

                case ImTextureStatus.Ok:
                default:
                    break;
            }
        }
    }

    private void CreateTexture(ImTextureDataPtr textureData)
    {
        var format = textureData.Format == ImTextureFormat.Rgba32
            ? TextureFormat.Rgba8Unorm
            : TextureFormat.R8Unorm; // Alpha8 - редкий путь, см. class-level комментарий про поддержку форматов

        var pixels = new ReadOnlySpan<byte>(textureData.GetPixels(), textureData.GetSizeInBytes());

        var handle = _device.CreateTexture(
            new TextureDescriptor(
                Width: (uint)textureData.Width,
                Height: (uint)textureData.Height,
                Format: format,
                Usage: TextureUsage.Sampled,
                DebugName: "ImGuiTexture"),
            pixels);

        var texId = new ImTextureID(_nextTextureId++);
        _textures[texId] = handle;

        // Обязательный вызов - см. комментарий у поля _textures. Без него
        // ImGui падает с ассертом "Backend must call SetTexID() after
        // handling ImTextureStatus_WantCreate request!" на первом же
        // ImDrawCmd, ссылающемся на эту текстуру.
        textureData.SetTexID(texId);
        textureData.SetStatus(ImTextureStatus.Ok);
    }

    private void DestroyTexture(ImTextureDataPtr textureData, bool markDestroyed)
    {
        if (_textures.Remove(textureData.TexID, out var handle))
        {
            _device.DestroyTexture(handle);
        }

        if (markDestroyed)
        {
            // Документация SetStatus: "Call after honoring a request. Never
            // modify Status directly!" - официальный BACKENDS.md явно требует
            // подтверждать WantDestroy этим вызовом (сам MonoGame-пример в
            // Hexa.NET.ImGui этот вызов почему-то пропускает - похоже, недочёт
            // примера, а не часть контракта).
            textureData.SetStatus(ImTextureStatus.Destroyed);
        }
    }

    private void BuildBuffers(ImDrawDataPtr drawData, out BufferHandle vertexBuffer, out BufferHandle indexBuffer)
    {
        int totalVtx = drawData.TotalVtxCount;
        int totalIdx = drawData.TotalIdxCount;

        if (_vertexScratch.Length < totalVtx)
            Array.Resize(ref _vertexScratch, Math.Max(totalVtx, _vertexScratch.Length * 2));
        if (_indexScratch.Length < totalIdx)
            Array.Resize(ref _indexScratch, Math.Max(totalIdx, _indexScratch.Length * 2));

        int vtxWrite = 0, idxWrite = 0;

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists.Data[n];

            var srcVtx = cmdList.VtxBuffer.Data;
            for (int i = 0; i < cmdList.VtxBuffer.Size; i++)
            {
                var v = srcVtx[i];
                uint packedColor = v.Col;
                float ca = ((packedColor >> 24) & 0xFF) / 255f;
                float cb = ((packedColor >> 16) & 0xFF) / 255f;
                float cg = ((packedColor >> 8) & 0xFF) / 255f;
                float cr = (packedColor & 0xFF) / 255f;

                _vertexScratch[vtxWrite + i] = new ImGuiVertex
                {
                    Position = new Vector4(v.Pos.X, v.Pos.Y, 0f, 1f),
                    Uv = new Vector4(v.Uv.X, v.Uv.Y, 0f, 0f),
                    Color = new Vector4(cr, cg, cb, ca)
                };
            }
            vtxWrite += cmdList.VtxBuffer.Size;

            // ImDrawIdx у стокового ImGui - unsigned short (16 бит). RHI
            // (OpenGlCommandList.DrawIndexed / VulkanCommandList.BindIndexBuffer)
            // жёстко ожидает 32-битные индексы - расширяем на CPU.
            // Если ваша сборка Hexa.NET.ImGui собрана с 32-битным ImDrawIdx
            // (нестандартный imconfig.h), замените ushort* ниже на uint*
            // и уберите расширение.
            var srcIdx = (ushort*)cmdList.IdxBuffer.Data;
            for (int i = 0; i < cmdList.IdxBuffer.Size; i++)
            {
                _indexScratch[idxWrite + i] = srcIdx[i];
            }
            idxWrite += cmdList.IdxBuffer.Size;
        }

        var vtxBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(
            _vertexScratch.AsSpan(0, totalVtx));
        var idxBytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(
            _indexScratch.AsSpan(0, totalIdx));

        vertexBuffer = _device.CreateBuffer(
            new BufferDescriptor(
                Size: (ulong)vtxBytes.Length,
                Usage: BufferUsage.Vertex,
                CpuVisible: true,
                DebugName: "ImGuiVertices"),
            vtxBytes);

        indexBuffer = _device.CreateBuffer(
            new BufferDescriptor(
                Size: (ulong)idxBytes.Length,
                Usage: BufferUsage.Index,
                CpuVisible: true,
                DebugName: "ImGuiIndices"),
            idxBytes);
    }

    private void DestroyFrameBuffers()
    {
        if (_vertexBuffer is { } vb)
        {
            _device.DestroyBuffer(vb);
            _vertexBuffer = null;
        }

        if (_indexBuffer is { } ib)
        {
            _device.DestroyBuffer(ib);
            _indexBuffer = null;
        }
    }

    public void Dispose()
    {
        _input.Dispose();

        DestroyFrameBuffers();

        foreach (var handle in _textures.Values)
            _device.DestroyTexture(handle);
        _textures.Clear();

        _device.DestroyPipeline(_pipeline);
        _device.DestroyShader(_vertexShader);
        _device.DestroyShader(_fragmentShader);
        _device.DestroySampler(_sampler);

        Hexa.NET.ImGui.ImGui.DestroyContext(_context);
    }
}
